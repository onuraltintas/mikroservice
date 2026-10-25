using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Notification.Application.Commands.ReplyToSupportRequest;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.ExternalServices;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Identity.API.IntegrationTests;

/// <summary>Support flow on real PostgreSQL with the Notification migrations.</summary>
public sealed class NotificationSupportFlowPostgresTests : IAsyncLifetime
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

        _connectionString = new NpgsqlConnectionStringBuilder(server) { Database = $"notification_test_{Guid.NewGuid():N}" }.ConnectionString;
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
    public async Task ReplyToSignedInUser_QueuesTheEmailAndStoresTheProcessedState()
    {
        var userId = Guid.NewGuid();
        var requestId = await SeedRequestAsync("member@example.test", userId);

        await using (var context = CreateContext())
        {
            var handler = new ReplyToSupportRequestHandler(context, Queue(context));
            (await handler.Handle(new ReplyToSupportRequestCommand(requestId, "Sorununuz çözüldü."), CancellationToken.None))
                .IsSuccess.Should().BeTrue();
        }

        await using (var context = CreateContext())
        {
            var stored = await context.SupportRequests.AsNoTracking().SingleAsync(x => x.Id == requestId);
            stored.IsProcessed.Should().BeTrue("the reply must not leave the request open");
            stored.AdminNote.Should().Be("Sorununuz çözüldü.");
            (await context.EmailDeliveries.CountAsync(x => x.SubjectUserId == userId)).Should().Be(1);
        }
    }

    [Fact]
    public async Task AnonymousForm_SendsAtMostThreeAcknowledgementsPerAddressPerDay()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            await using var context = CreateContext();
            var handler = new SubmitSupportRequestHandler(context, Queue(context));
            var result = await handler.Handle(new SubmitSupportRequestCommand(
                "Kazandınız", "www.evil.example", "Victim@Example.test", "Konu", "Mesaj", $"key-{attempt}"), CancellationToken.None);
            result.IsSuccess.Should().BeTrue("requests are still stored and forwarded to staff");
        }

        await using (var context = CreateContext())
        {
            (await context.SupportRequests.CountAsync(x => x.Email == "victim@example.test")).Should().Be(5);
            var acknowledgements = await context.EmailDeliveries.Where(x => x.ConsumerType == "SupportAcknowledgement").ToListAsync();
            acknowledgements.Should().HaveCount(SubmitSupportRequestHandler.MaxAcknowledgementsPerAddressPerDay);
            acknowledgements.Should().OnlyContain(x => !x.Subject.Contains("Kazandınız") && !x.Subject.Contains("evil"));
        }
    }

    private NotificationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<NotificationDbContext>().UseNpgsql(_connectionString).Options);

    private static EmailDeliveryQueue Queue(NotificationDbContext context) =>
        new(context, new EphemeralDataProtectionProvider());

    private async Task<Guid> SeedRequestAsync(string email, Guid userId)
    {
        await using var context = CreateContext();
        var request = new SupportRequest(Guid.NewGuid(), "Ada", "Lovelace", email, "Giriş", "Giriş yapamıyorum", "seed", userId);
        context.SupportRequests.Add(request);
        await context.SaveChangesAsync();
        return request.Id;
    }
}

public sealed class SupportForwardResultTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, SupportForwardResult.Sent)]
    [InlineData(HttpStatusCode.BadRequest, SupportForwardResult.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, SupportForwardResult.Rejected)]
    [InlineData(HttpStatusCode.RequestTimeout, SupportForwardResult.Transient)]
    [InlineData(HttpStatusCode.TooManyRequests, SupportForwardResult.Transient)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SupportForwardResult.Transient)]
    public async Task IdentityAnswers_AreClassifiedForRetry(HttpStatusCode status, SupportForwardResult expected)
    {
        var service = Create(new FixedHandler(_ => new HttpResponseMessage(status)), apiKey: "k");

        (await service.ForwardSupportRequestAsync(Command(), Guid.NewGuid())).Should().Be(expected);
    }

    [Fact]
    public async Task NetworkFailure_IsTransient_AndMissingKey_IsRejected()
    {
        (await Create(new FixedHandler(_ => throw new HttpRequestException("down")), apiKey: "k")
            .ForwardSupportRequestAsync(Command(), Guid.NewGuid())).Should().Be(SupportForwardResult.Transient);
        (await Create(new FixedHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)), apiKey: null)
            .ForwardSupportRequestAsync(Command(), Guid.NewGuid())).Should().Be(SupportForwardResult.Rejected);
    }

    private static SubmitSupportRequestCommand Command() => new("Ada", "Lovelace", "a@example.test", "Konu", "Mesaj", "key");

    private static IdentityInternalService Create(HttpMessageHandler handler, string? apiKey)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Services:IdentityService"] = "http://identity.test",
            ["Internal:ServiceApiKey"] = apiKey,
        }).Build();
        return new IdentityInternalService(new HttpClient(handler), configuration, NullLogger<IdentityInternalService>.Instance);
    }

    private sealed class FixedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
