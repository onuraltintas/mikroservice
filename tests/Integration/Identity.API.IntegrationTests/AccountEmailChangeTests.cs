using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.Commands.ChangeEmail;
using Identity.Application.Commands.ChangePassword;
using Identity.Application.Commands.UpdateUserProfile;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// E5 (#22, live test 06.11.2026): Profil ve ayarlar › Hesabım. A student or parent changes the e-mail address only
/// through a link sent to the new address (the current password is asked first); the old address is told after the
/// change. Accounts without a password (Google sign-in) are told so instead of "Mevcut şifre hatalı".
/// </summary>
public sealed class AccountEmailChangeTests
{
    private const string Password = "Current-Password-1!";

    [Fact]
    public async Task RequestKeepsTheAddressAndMailsALinkToTheNewAddress()
    {
        await using var f = await Fixture.CreateAsync();

        var result = await f.Request(Password, "  Yeni.Adres@Example.test ");

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        result.Value.PendingEmail.Should().Be("yeni.adres@example.test");
        result.Value.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(24), TimeSpan.FromMinutes(1));
        var user = await f.Reload();
        user.Email.Should().Be("ogrenci@example.test", "nothing changes before the new address is confirmed");
        user.PendingEmail.Should().Be("yeni.adres@example.test");
        var mail = f.Published.OfType<UserEmailChangeRequestedEvent>().Should().ContainSingle().Subject;
        mail.UserId.Should().Be(user.Id);
        mail.NewEmail.Should().Be("yeni.adres@example.test");
        mail.Role.Should().Be("Student");
        mail.Product.Should().Be("coaching");
        mail.ConfirmationToken.Should().NotBeNullOrWhiteSpace();
        user.PendingEmailToken.Should().NotBe(mail.ConfirmationToken, "only a digest of the link value is stored");
        user.EmailChangeTokenMatches(mail.ConfirmationToken).Should().BeTrue();
    }

    [Theory]
    [InlineData("Wrong-Password-1!", "yeni@example.test", "Auth.InvalidCredentials")]
    [InlineData(Password, "ogrenci@example.test", "Account.SameEmail")]
    [InlineData(Password, "OGRENCI@example.test", "Account.SameEmail")]
    [InlineData(Password, "baskasi@example.test", "Account.EmailInUse")]
    public async Task RequestIsRefusedWithoutAMailOrAChange(string password, string newEmail, string code)
    {
        await using var f = await Fixture.CreateAsync();

        var result = await f.Request(password, newEmail);

        result.Error.Code.Should().Be(code);
        result.Error.Description.Should().NotBeNullOrWhiteSpace();
        f.Published.Should().BeEmpty();
        (await f.Reload()).PendingEmail.Should().BeNull();
    }

    [Fact]
    public async Task AnAccountWithoutAPasswordIsToldToSetOneFirst()
    {
        await using var f = await Fixture.CreateAsync(withPassword: false);

        var request = await f.Request("anything", "yeni@example.test");
        var change = await new ChangePasswordCommandHandler(f.Users, new PasswordHasher(), f.UnitOfWork, f.CurrentUser)
            .Handle(new ChangePasswordCommand("anything", "Replacement-Password-1!"), default);

        request.Error.Code.Should().Be("Account.PasswordNotSet");
        change.Error.Code.Should().Be("Account.PasswordNotSet");
        change.Error.Description.Should().Contain("Google");
        f.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task ASecondRequestWithinAMinuteIsRefused()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "ilk@example.test")).IsSuccess.Should().BeTrue();

        var again = await f.Request(Password, "ikinci@example.test");

        again.Error.Code.Should().Be("Account.EmailChangeTooSoon");
        f.Published.Should().ContainSingle();
        (await f.Reload()).PendingEmail.Should().Be("ilk@example.test");
    }

    [Fact]
    public async Task ConfirmingTheLinkChangesTheAddressAndTellsTheOldOne()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        var token = f.Published.OfType<UserEmailChangeRequestedEvent>().Single().ConfirmationToken;
        var before = await f.Reload();
        before.GeneratePasswordResetToken();
        await f.Context.SaveChangesAsync();

        var result = await f.Confirm(before.Id, token);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        var user = await f.Reload();
        user.Email.Should().Be("yeni@example.test");
        user.EmailConfirmed.Should().BeTrue();
        user.PendingEmail.Should().BeNull();
        user.PendingEmailToken.Should().BeNull();
        user.PasswordResetToken.Should().BeNull("a reset link sent to the old address no longer works");
        var notice = f.Published.OfType<UserEmailChangedEvent>().Should().ContainSingle().Subject;
        notice.PreviousEmail.Should().Be("ogrenci@example.test");
        notice.NewEmail.Should().Be("yeni@example.test");
        notice.Product.Should().Be("coaching");

        (await f.Confirm(user.Id, token)).Error.Code.Should().Be("EmailChange.InvalidLink", "the link works once");
    }

    [Fact]
    public async Task AWrongOrExpiredLinkChangesNothing()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        var token = f.Published.OfType<UserEmailChangeRequestedEvent>().Single().ConfirmationToken;
        var user = await f.Reload();

        (await f.Confirm(user.Id, token + "x")).Error.Code.Should().Be("EmailChange.InvalidLink");
        (await f.Confirm(Guid.NewGuid(), token)).Error.Code.Should().Be("EmailChange.InvalidLink");
        typeof(User).GetProperty(nameof(User.PendingEmailExpiresAt))!.SetValue(user, DateTime.UtcNow.AddMinutes(-1));
        await f.Context.SaveChangesAsync();
        (await f.Confirm(user.Id, token)).Error.Code.Should().Be("EmailChange.InvalidLink");

        (await f.Reload()).Email.Should().Be("ogrenci@example.test");
        f.Published.OfType<UserEmailChangedEvent>().Should().BeEmpty();
    }

    [Fact]
    public async Task AnAddressTakenAfterTheRequestIsNotTakenOver()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        var token = f.Published.OfType<UserEmailChangeRequestedEvent>().Single().ConfirmationToken;
        f.Context.Users.Add(User.Create(Guid.NewGuid(), "yeni@example.test", "Geç", "Kayıt"));
        await f.Context.SaveChangesAsync();

        var result = await f.Confirm(f.UserId, token);

        result.Error.Code.Should().Be("Account.EmailInUse");
        (await f.Reload()).Email.Should().Be("ogrenci@example.test");
    }

    [Fact]
    public async Task APendingChangeCanBeCancelled()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        var token = f.Published.OfType<UserEmailChangeRequestedEvent>().Single().ConfirmationToken;

        (await new CancelEmailChangeCommandHandler(f.Users, f.UnitOfWork, f.CurrentUser).Handle(new CancelEmailChangeCommand(), default))
            .IsSuccess.Should().BeTrue();

        (await f.Reload()).PendingEmail.Should().BeNull();
        (await f.Confirm(f.UserId, token)).Error.Code.Should().Be("EmailChange.InvalidLink");
    }

    [Fact]
    public void ErasureRemovesAPendingAddress()
    {
        var user = User.Create(Guid.NewGuid(), "a@example.test", "A", "B");
        user.RequestEmailChange("yeni@example.test", DateTime.UtcNow);

        user.AnonymizeForErasure();

        user.PendingEmail.Should().BeNull();
        user.PendingEmailToken.Should().BeNull();
    }

    [Theory]
    [InlineData("", "yeni@example.test", "CurrentPassword")]
    [InlineData(Password, "", "NewEmail")]
    [InlineData(Password, "adres-degil", "NewEmail")]
    public void TheRequestIsValidatedWithTurkishMessages(string password, string newEmail, string property)
    {
        var errors = new RequestEmailChangeCommandValidator().Validate(new RequestEmailChangeCommand(password, newEmail)).Errors;

        errors.Should().ContainSingle(error => error.PropertyName == property)
            .Which.ErrorMessage.Should().MatchRegex("(?i)[çğıöşü]|e-posta|mevcut şifre");
    }

    [Fact]
    public void NameUpdatesAreValidatedInTurkish()
    {
        var errors = new UpdateUserProfileCommandValidator()
            .Validate(new UpdateUserProfileCommand(new string('a', 101), " ", null, null, null, null, null, null, null, null)).Errors;

        errors.Select(error => error.ErrorMessage).Should().Contain(message => message.Contains("Ad en fazla"))
            .And.Contain(message => message.Contains("Soyad"));
    }

    [Fact]
    public async Task TheOwnProfileTellsWhetherThereIsAPasswordAndAPendingAddress()
    {
        await using var f = await Fixture.CreateAsync(withPassword: false);
        f.Context.Users.Single(u => u.Id == f.UserId).RequestEmailChange("yeni@example.test", DateTime.UtcNow);
        await f.Context.SaveChangesAsync();

        var own = await new GetUserProfileQueryHandler(f.Users, new TeacherRepository(f.Context), new StudentRepository(f.Context),
            new InstitutionRepository(f.Context), f.CurrentUser).Handle(new GetUserProfileQuery(f.UserId), default);

        own.Value.HasPassword.Should().BeFalse();
        own.Value.PendingEmail.Should().Be("yeni@example.test");
        own.Value.PendingEmailExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public void TheEndpointsAreTheSignedInUsersAndTheLinkIsAnonymous()
    {
        var request = typeof(UserController).GetMethod(nameof(UserController.RequestEmailChange))!;
        request.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("me/email-change");
        request.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var cancel = typeof(UserController).GetMethod(nameof(UserController.CancelEmailChange))!;
        cancel.GetCustomAttribute<HttpDeleteAttribute>()!.Template.Should().Be("me/email-change");
        cancel.GetCustomAttribute<AuthorizeAttribute>().Should().NotBeNull();
        var confirm = typeof(AuthController).GetMethod(nameof(AuthController.ConfirmEmailChange))!;
        confirm.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("confirm-email-change");
        confirm.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
    }

    // The e-mails: the link opens the Coaching site's confirmation page (management accounts: the admin panel);
    // the notice goes to the old address and does not spell out the new one in full.
    [Theory]
    [InlineData("Student", "https://onuraltintas.net/auth/confirm-email-change?userId={0}&token=abc%2B1")]
    [InlineData("Parent", "https://onuraltintas.net/auth/confirm-email-change?userId={0}&token=abc%2B1")]
    [InlineData("SystemAdmin", "https://eduivme.com/auth/confirm-email-change?userId={0}&token=abc%2B1")]
    public async Task TheConfirmationMailGoesToTheNewAddressWithTheCoachingLink(string role, string link)
    {
        var userId = Guid.NewGuid();
        var mail = await RunAsync<UserEmailChangeRequestedConsumer>(
            new UserEmailChangeRequestedEvent(userId, "yeni@example.test", "Ada", "<b>Kaya</b>", "abc+1", role) { Product = "coaching" });

        mail.Recipient.Should().Be("yeni@example.test");
        mail.Body.Should().Contain($"href='{string.Format(link, userId)}'");
        mail.Body.Should().Contain("Eduİvme").And.NotContain("Hızlı Okuma").And.NotContain("<b>Kaya</b>");
        mail.Subject.Should().Contain("E-posta");
    }

    [Fact]
    public async Task TheOldAddressIsToldAboutTheChange()
    {
        var mail = await RunAsync<UserEmailChangedConsumer>(
            new UserEmailChangedEvent(Guid.NewGuid(), "eski@example.test", "yeniadres@example.test", "Ada", "Kaya", "Student") { Product = "coaching" });

        mail.Recipient.Should().Be("eski@example.test");
        mail.Body.Should().Contain("y***@example.test").And.NotContain("yeniadres@example.test");
        mail.Body.Should().Contain("https://onuraltintas.net/auth/support");
        mail.Body.Should().Contain("Eduİvme").And.NotContain("Hızlı Okuma");
    }

    private sealed record Mail(string Recipient, string Subject, string Body);

    private static async Task<Mail> RunAsync<TConsumer>(object message) where TConsumer : class, IConsumer
    {
        var queue = new RecordingQueue();
        var services = new ServiceCollection();
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<NotificationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<INotificationDbContext>(provider => provider.GetRequiredService<NotificationDbContext>());
        services.AddSingleton<IEmailDeliveryQueue>(queue);
        services.Configure<PublicAppUrlOptions>(options =>
        {
            options.BaseUrl = "https://onuraltintas.net";
            options.SpeedReadingBaseUrl = "https://masterhizliokuma.com";
            options.AdminPanelUrl = "https://eduivme.com";
        });
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.UseSuiteTimeouts();
            configurator.AddConsumer<TConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });
        await using var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message, message.GetType());
            await TestHarnessTimeouts.UntilAsync(() => !queue.Messages.IsEmpty);
            return queue.Messages.Should().ContainSingle().Subject;
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed class RecordingQueue : IEmailDeliveryQueue
    {
        public System.Collections.Concurrent.ConcurrentQueue<Mail> Messages { get; } = new();

        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body,
            Guid? subjectUserId = null, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(new Mail(recipient, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public IdentityDbContext Context { get; private init; } = null!;
        public UserRepository Users => new(Context);
        public UnitOfWork UnitOfWork => new(Context);
        public Guid UserId { get; private init; }
        public ICurrentUserService CurrentUser => new Current(UserId);
        public List<object> Published { get; } = [];

        public static async Task<Fixture> CreateAsync(bool withPassword = true)
        {
            var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var user = User.Create(Guid.NewGuid(), "ogrenci@example.test", "Ada", "Kaya");
            user.ConfirmEmail();
            if (withPassword)
            {
                new PasswordHasher().CreatePasswordHash(Password, out var hash, out var salt);
                user.SetPassword(hash, salt);
            }
            user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            var role = Role.Create("Student", "Student", true);
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, PlatformProduct.Coaching));
            context.Roles.Add(role);
            context.Users.Add(user);
            context.Users.Add(User.Create(Guid.NewGuid(), "baskasi@example.test", "Başka", "Hesap"));
            await context.SaveChangesAsync();
            return new Fixture { Context = context, UserId = user.Id };
        }

        public Task<EduPlatform.Shared.Kernel.Results.Result<EmailChangeRequested>> Request(string password, string newEmail) =>
            new RequestEmailChangeCommandHandler(Users, new PasswordHasher(), UnitOfWork, Publisher(), CurrentUser)
                .Handle(new RequestEmailChangeCommand(password, newEmail, PlatformProduct.Coaching), default);

        public Task<EduPlatform.Shared.Kernel.Results.Result> Confirm(Guid userId, string token) =>
            new ConfirmEmailChangeCommandHandler(Users, UnitOfWork, Publisher())
                .Handle(new ConfirmEmailChangeCommand(userId, token), default);

        public async Task<User> Reload()
        {
            Context.ChangeTracker.Clear();
            return await Context.Users.Include(u => u.Roles).ThenInclude(r => r.Role).SingleAsync(u => u.Id == UserId);
        }

        private IPublishEndpoint Publisher()
        {
            var proxy = DispatchProxy.Create<IPublishEndpoint, PublishProxy>();
            ((PublishProxy)(object)proxy).Published = Published;
            return proxy;
        }

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }

    public class PublishProxy : DispatchProxy
    {
        public List<object> Published = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Published.Add(args![0]!);
            return Task.CompletedTask;
        }
    }

    private sealed class Current(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => ["Student"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
