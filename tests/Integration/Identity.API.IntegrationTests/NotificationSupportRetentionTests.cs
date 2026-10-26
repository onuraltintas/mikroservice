using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.API.Security;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Notification.API.Controllers;
using Notification.Application.Commands.ManageNotifications;
using Notification.Application.Commands.ReplyToSupportRequest;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;
using Notification.Application.Support;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Identity.API.IntegrationTests;

/// <summary>Deleting support requests (by an admin and after six months) on real PostgreSQL.</summary>
public sealed class NotificationSupportRetentionTests : IAsyncLifetime
{
    private PostgreSqlContainer? _container;
    private string _connectionString = string.Empty;

    public async Task InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        string server;
        if (!string.IsNullOrWhiteSpace(external))
        {
            server = external;
        }
        else
        {
            _container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
            await _container.StartAsync();
            server = _container.GetConnectionString();
        }

        _connectionString = new NpgsqlConnectionStringBuilder(server) { Database = $"notification_support_{Guid.NewGuid():N}" }.ConnectionString;
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
        }

        if (_container is not null) await _container.DisposeAsync();
    }

    [Fact]
    public async Task AdminDelete_RemovesTheRequestAndEveryCopyOfTheSendersData()
    {
        var target = await SeedAnsweredRequestAsync("ada@example.test", "Çözüldü.");
        var other = await SeedAnsweredRequestAsync("grace@example.test", "Tamam.");

        await using (var context = CreateContext())
        {
            var result = await new DeleteSupportRequestCommandHandler(context)
                .Handle(new DeleteSupportRequestCommand(target), CancellationToken.None);
            result.IsSuccess.Should().BeTrue();
        }

        await using var check = CreateContext();
        (await check.SupportRequests.AnyAsync(x => x.Id == target)).Should().BeFalse();
        (await check.Notifications.AnyAsync(x => x.RelatedEntityId == target.ToString())).Should().BeFalse("staff copies hold the name and message");
        (await check.SupportForwardDeliveries.AnyAsync(x => x.SupportRequestId == target)).Should().BeFalse();
        (await check.EmailDeliveries.AnyAsync(x => x.Recipient == "ada@example.test")).Should().BeFalse();

        (await check.SupportRequests.AnyAsync(x => x.Id == other)).Should().BeTrue();
        (await check.Notifications.CountAsync(x => x.RelatedEntityId == other.ToString())).Should().Be(1);
        (await check.EmailDeliveries.CountAsync(x => x.Recipient == "grace@example.test")).Should().Be(2);
        (await check.Notifications.CountAsync(x => x.Type == "Info")).Should().Be(2, "unrelated notifications stay");
    }

    [Fact]
    public async Task AdminDelete_OfAnUnknownRequest_IsNotFound()
    {
        await using var context = CreateContext();

        var result = await new DeleteSupportRequestCommandHandler(context)
            .Handle(new DeleteSupportRequestCommand(Guid.NewGuid()), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task AnsweredRequests_AreDeletedSixMonthsAfterTheAnswer_OpenOnesAreKept()
    {
        var expired = await SeedAnsweredRequestAsync("old@example.test", "Eski yanıt.");
        var recent = await SeedAnsweredRequestAsync("new@example.test", "Yeni yanıt.");
        var open = await SeedOpenRequestAsync("open@example.test");
        var now = DateTime.UtcNow;
        await using (var context = CreateContext())
        {
            await context.SupportRequests.Where(x => x.Id == expired)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.UpdatedAt, now.AddMonths(-6).AddDays(-1)));
            await context.SupportRequests.Where(x => x.Id == recent)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.UpdatedAt, now.AddMonths(-5)));
            await context.SupportRequests.Where(x => x.Id == open)
                .ExecuteUpdateAsync(x => x.SetProperty(r => r.CreatedAt, now.AddYears(-2)));
        }

        await using (var context = CreateContext())
        {
            await EmailDeliveryWorker.PurgeExpiredRecordsAsync(context, now, CancellationToken.None);
        }

        await using var check = CreateContext();
        (await check.SupportRequests.Select(x => x.Id).ToListAsync()).Should().BeEquivalentTo([recent, open]);
        (await check.Notifications.AnyAsync(x => x.RelatedEntityId == expired.ToString())).Should().BeFalse();
        (await check.EmailDeliveries.AnyAsync(x => x.Recipient == "old@example.test")).Should().BeFalse();
    }

    private NotificationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(_connectionString).Options);

    private async Task<Guid> SeedOpenRequestAsync(string email)
    {
        await using var context = CreateContext();
        var request = new SupportRequest(Guid.NewGuid(), "Ada", "Lovelace", email, "Konu", "Mesaj", Guid.NewGuid().ToString());
        context.SupportRequests.Add(request);
        context.SupportForwardDeliveries.Add(SupportForwardDelivery.Create(request.Id));
        // What Identity's forward creates for each staff member, plus an unrelated notification.
        context.Notifications.Add(NotificationItem.Create(Guid.NewGuid(), "Yeni Destek Talebi", $"Ada Lovelace ({email}) …",
            SupportRequestRemoval.StaffNotificationType, request.Id.ToString(), null));
        context.Notifications.Add(NotificationItem.Create(Guid.NewGuid(), "Başka", "Başka", "Info", null, null));
        context.EmailDeliveries.Add(EmailDelivery.Create(request.Id, SupportRequestRemoval.AcknowledgementConsumerType, email, "Alındı", "b"));
        await context.SaveChangesAsync();
        return request.Id;
    }

    private async Task<Guid> SeedAnsweredRequestAsync(string email, string reply)
    {
        var id = await SeedOpenRequestAsync(email);
        await using var context = CreateContext();
        var request = await context.SupportRequests.SingleAsync(x => x.Id == id);
        request.Process(reply);
        context.EmailDeliveries.Add(EmailDelivery.Create(ReplyToSupportRequestHandler.ReplyMessageId(id, reply),
            SupportRequestRemoval.ReplyConsumerType, email, "RE", "b"));
        await context.SaveChangesAsync();
        return id;
    }
}

public sealed class NotificationSupportAccessTests
{
    [Fact]
    public void Delete_NeedsItsOwnPermission_WhichFollowsTheContentMfaPolicy()
    {
        var action = typeof(SupportAdminController).GetMethod(nameof(SupportAdminController.Delete))!;

        action.GetCustomAttribute<HttpDeleteAttribute>().Should().NotBeNull();
        action.GetCustomAttribute<HasPermissionAttribute>()!.Permission.Should().Be(PlatformPermissions.Support.Delete);
        MfaOperationCategories.TryGetManagementCategory(PlatformPermissions.Support.Delete, out var category).Should().BeTrue();
        category.Should().Be(MfaOperationCategories.Cms);
        PlatformPermissions.GetAll().Should().Contain(PlatformPermissions.Support.Delete);
        Identity.Domain.Constants.Permissions.GetAll().Should().Contain(PlatformPermissions.Support.Delete,
            "the Identity seeder creates every listed permission and SystemAdmin receives them all");
    }

    [Theory]
    [InlineData(false, false, 403)]
    [InlineData(false, true, 200)]
    [InlineData(true, false, 200)]
    public async Task AnonymousForm_NeedsAnAcceptedCaptcha_SignedInUsersDoNot(bool signedIn, bool captchaAccepted, int expected)
    {
        var mediator = new CountingMediator();
        var controller = new SupportController(mediator.Mediator)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        if (signedIn)
        {
            controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Guid.NewGuid().ToString())], "Bearer"));
        }
        controller.HttpContext.Request.Headers[SupportController.CaptchaTokenHeader] = "token";
        var identity = new CaptchaIdentity(captchaAccepted);

        var response = await controller.Submit(
            new SubmitSupportRequestCommand("Ada", "Lovelace", "a@example.test", "Konu", "Mesaj", null), identity, CancellationToken.None);

        (response as IStatusCodeActionResult)!.StatusCode.Should().Be(expected);
        mediator.Calls.Should().Be(expected == 200 ? 1 : 0, "a rejected form never reaches the handler");
        identity.Tokens.Should().Equal(signedIn ? [] : ["token"]);
    }

    [Theory]
    [InlineData("support_submit", true, true)]
    [InlineData("support_submit", false, false)]
    [InlineData("auth_login", true, null)]
    [InlineData(null, true, null)]
    public async Task IdentityCaptchaEndpoint_VerifiesOnlyAllowedActions(string? action, bool verifierAnswer, bool? accepted)
    {
        var verifier = new FixedVerifier(verifierAnswer);
        var controller = new InternalCaptchaController(verifier);

        var result = await controller.Verify(new InternalCaptchaController.CaptchaVerificationRequest("token", action), CancellationToken.None);

        if (accepted is null)
        {
            result.Result.Should().BeOfType<BadRequestResult>();
            verifier.Actions.Should().BeEmpty();
        }
        else
        {
            ((result.Result as OkObjectResult)!.Value as InternalCaptchaController.CaptchaVerificationResponse)!
                .Accepted.Should().Be(accepted.Value);
            verifier.Actions.Should().Equal(action);
        }

        typeof(InternalCaptchaController).GetMethod(nameof(InternalCaptchaController.Verify))!
            .GetCustomAttribute<Identity.API.InternalServiceKeyAttribute>().Should().NotBeNull();
    }

    private sealed class FixedVerifier(bool answer) : IAuthRecaptchaVerifier
    {
        public List<string> Actions { get; } = [];

        public Task<bool> VerifyAsync(string? token, string action, CancellationToken cancellationToken)
        {
            Actions.Add(action);
            return Task.FromResult(answer);
        }
    }

    private sealed class CaptchaIdentity(bool accepted) : IIdentityInternalService
    {
        public List<string?> Tokens { get; } = [];

        public Task<SupportForwardResult> ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId,
            CancellationToken cancellationToken = default) => Task.FromResult(SupportForwardResult.Sent);

        public Task<bool> VerifySupportCaptchaAsync(string? token, CancellationToken cancellationToken = default)
        {
            Tokens.Add(token);
            return Task.FromResult(accepted);
        }
    }

    private sealed class CountingMediator
    {
        public int Calls;
        public IMediator Mediator { get; }

        public CountingMediator()
        {
            Mediator = DispatchProxy.Create<IMediator, Proxy>();
            ((Proxy)(object)Mediator).Owner = this;
        }

        public class Proxy : DispatchProxy
        {
            public CountingMediator Owner = null!;

            protected override object? Invoke(MethodInfo? method, object?[]? args)
            {
                Owner.Calls++;
                return Task.FromResult(Result.Success(Guid.NewGuid()));
            }
        }
    }
}
