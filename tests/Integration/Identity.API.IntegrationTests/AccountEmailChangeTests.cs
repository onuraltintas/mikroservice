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
        // Review L9: accounts provisioned by an administrator have no password either, so the wording is neutral.
        change.Error.Description.Should().Contain("henüz bir şifre belirlenmemiş");
        request.Error.Description.Should().Contain("henüz bir şifre belirlenmemiş");
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

    // Paket F L6: with two-step verification on, the password alone does not start a change.
    [Theory]
    [InlineData(null, "Account.MfaCodeRequired")]
    [InlineData("", "Account.MfaCodeRequired")]
    [InlineData("000000", "Auth.InvalidMfaCode")]
    public async Task WithTwoStepVerificationTheCodeIsRequired(string? code, string expected)
    {
        await using var f = await Fixture.CreateAsync(withMfa: true);

        var result = await f.Request(Password, "yeni@example.test", code);

        result.Error.Code.Should().Be(expected);
        result.Error.Description.Should().NotBeNullOrWhiteSpace();
        f.Published.Should().BeEmpty();
        (await f.Reload()).PendingEmail.Should().BeNull();
    }

    [Fact]
    public async Task WithTwoStepVerificationAValidCodeStartsTheChangeAndIsUsedOnce()
    {
        await using var f = await Fixture.CreateAsync(withMfa: true);

        var result = await f.Request(Password, "yeni@example.test", " 123456 ");

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : "");
        var user = await f.Reload();
        user.PendingEmail.Should().Be("yeni@example.test");
        user.LastAcceptedMfaTimeStep.Should().Be(42);
        f.Published.OfType<UserEmailChangeRequestedEvent>().Should().ContainSingle();
        // The same code cannot be replayed (checked before the one-minute limit).
        (await f.Request(Password, "baska@example.test", "123456")).Error.Code.Should().Be("Auth.InvalidMfaCode");
    }

    [Fact]
    public async Task WrongCodesCountTowardsTheLockout()
    {
        await using var f = await Fixture.CreateAsync(withMfa: true);
        for (var i = 0; i < 5; i++)
            (await f.Request(Password, "yeni@example.test", "000000")).Error.Code.Should().Be("Auth.InvalidMfaCode");

        // Locked: even the right code is refused for now.
        (await f.Request(Password, "yeni@example.test", "123456")).Error.Code.Should().Be("Auth.MfaLocked");
        f.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task AWrongPasswordIsReportedBeforeTheCodeAndDoesNotCountAsACodeFailure()
    {
        await using var f = await Fixture.CreateAsync(withMfa: true);

        (await f.Request("Wrong-Password-1!", "yeni@example.test", "000000")).Error.Code.Should().Be("Auth.InvalidCredentials");

        (await f.Reload()).MfaFailedAttempts.Should().Be(0);
    }

    [Fact]
    public async Task WithoutTwoStepVerificationNoCodeIsAsked()
    {
        await using var f = await Fixture.CreateAsync();

        (await f.Request(Password, "yeni@example.test", "garbage")).IsSuccess.Should().BeTrue();
    }

    // Review L3: the one-minute limit is checked before the address, so the form cannot be used to probe addresses.
    [Fact]
    public async Task TheMinuteLimitComesBeforeTheAddressCheck()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "ilk@example.test")).IsSuccess.Should().BeTrue();

        (await f.Request(Password, "baskasi@example.test")).Error.Code.Should().Be("Account.EmailChangeTooSoon");
    }

    // Review M1 (a, c): confirming ends every session of the account and removes its Google sign-in, which belonged to
    // the old address. (Only an account with a password can ask for a change, so nobody is locked out.)
    [Fact]
    public async Task ConfirmingEndsTheSessionsAndRemovesTheGoogleSignIn()
    {
        await using var f = await Fixture.CreateAsync();
        var user = await f.Reload();
        f.Context.RefreshTokens.Add(RefreshToken.Create(user.Id, $"refresh-{Guid.NewGuid():N}", DateTime.UtcNow.AddDays(7), "127.0.0.1"));
        f.Context.Set<UserLogin>().Add(UserLogin.Create(user.Id, "Google", "google-subject", "Google"));
        await f.Context.SaveChangesAsync();
        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        var token = f.Published.OfType<UserEmailChangeRequestedEvent>().Single().ConfirmationToken;

        (await f.Confirm(f.UserId, token)).IsSuccess.Should().BeTrue();

        f.Context.ChangeTracker.Clear();
        (await f.Context.RefreshTokens.Where(t => t.UserId == f.UserId).ToListAsync()).Should().OnlyContain(t => t.RevokedAt != null);
        (await f.Context.Set<UserLogin>().CountAsync(l => l.UserId == f.UserId)).Should().Be(0);
    }

    // Review M1 (b): the confirmation page shows which account changes, with only the masked current address.
    [Fact]
    public async Task ThePreviewShowsOnlyTheMaskedCurrentAddress()
    {
        await using var f = await Fixture.CreateAsync();
        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        var token = f.Published.OfType<UserEmailChangeRequestedEvent>().Single().ConfirmationToken;
        var handler = new GetEmailChangePreviewQueryHandler(f.Users);

        var preview = await handler.Handle(new GetEmailChangePreviewQuery(f.UserId, token), default);

        preview.Value.CurrentEmailMasked.Should().Be("o***@example.test");
        (await handler.Handle(new GetEmailChangePreviewQuery(f.UserId, token + "x"), default)).Error.Code.Should().Be("EmailChange.InvalidLink");
        (await f.Reload()).PendingEmail.Should().Be("yeni@example.test", "looking does not confirm");
    }

    // Review M2: a password the owner never chose (Google registration, administrator provisioning) does not count.
    [Fact]
    public void APasswordTheOwnerNeverChoseDoesNotCount()
    {
        var user = User.Create(Guid.NewGuid(), "a@example.test", "A", "B");
        new PasswordHasher().CreatePasswordHash("Generated-Password-1!", out var hash, out var salt);

        user.SetPassword(hash, salt, knownToOwner: false);
        user.HasPassword.Should().BeFalse();

        user.SetPassword(hash, salt);
        user.HasPassword.Should().BeTrue();
    }

    [Fact]
    public async Task AGoogleRegistrationHasNoPassword()
    {
        await using var f = await Fixture.CreateAsync();
        var service = new Identity.Infrastructure.Services.LocalIdentityService(f.Users, new PasswordHasher(), f.UnitOfWork,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<Identity.Infrastructure.Services.LocalIdentityService>.Instance,
            null!, new RoleRepository(f.Context), f.Context, f.CurrentUser);

        var created = await service.RegisterExternalUserAsync("google@example.test", "Ada", "Google", default);

        f.Context.ChangeTracker.Clear();
        var user = await f.Context.Users.SingleAsync(u => u.Id == created.Value);
        user.HasPassword.Should().BeFalse();
        user.PasswordHash.Should().BeEmpty();
        var own = await new GetUserProfileQueryHandler(f.Users, new TeacherRepository(f.Context), new StudentRepository(f.Context),
            new InstitutionRepository(f.Context), new Current(user.Id)).Handle(new GetUserProfileQuery(user.Id), default);
        own.Value.HasPassword.Should().BeFalse();
    }

    // Review M2: an account linked to Google whose password was only marked unknown can still use it if the owner knows it.
    [Fact]
    public async Task AKnownPasswordStillWorksWhenItWasMarkedUnknown()
    {
        await using var f = await Fixture.CreateAsync();
        var user = await f.Reload();
        new PasswordHasher().CreatePasswordHash(Password, out var hash, out var salt);
        user.SetPassword(hash, salt, knownToOwner: false);
        await f.Context.SaveChangesAsync();

        (await f.Request(Password, "yeni@example.test")).IsSuccess.Should().BeTrue();
        (await f.Reload()).HasPassword.Should().BeTrue("entering it proves the owner knows it");
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
        var preview = typeof(AuthController).GetMethod(nameof(AuthController.PreviewEmailChange))!;
        preview.GetCustomAttribute<HttpPostAttribute>()!.Template.Should().Be("email-change-preview");
        preview.GetCustomAttribute<AllowAnonymousAttribute>().Should().NotBeNull();
        // Review L10: the request carries no product; the e-mail is always the Coaching site's.
        typeof(RequestEmailChangeCommand).GetProperty("Product").Should().BeNull();
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

        public static async Task<Fixture> CreateAsync(bool withPassword = true, bool withMfa = false)
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
            if (withMfa) user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
            var role = Role.Create("Student", "Student", true);
            user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, PlatformProduct.Coaching));
            context.Roles.Add(role);
            context.Users.Add(user);
            context.Users.Add(User.Create(Guid.NewGuid(), "baskasi@example.test", "Başka", "Hesap"));
            await context.SaveChangesAsync();
            return new Fixture { Context = context, UserId = user.Id };
        }

        public Task<EduPlatform.Shared.Kernel.Results.Result<EmailChangeRequested>> Request(string password, string newEmail, string? mfaCode = null) =>
            new RequestEmailChangeCommandHandler(Users, new PasswordHasher(), UnitOfWork, Publisher(), CurrentUser, new TotpStub())
                .Handle(new RequestEmailChangeCommand(password, newEmail, mfaCode), default);

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

    /// <summary>"123456" is the code of time step 42 and "654321" of step 43; anything else is wrong.</summary>
    private sealed class TotpStub : Identity.Application.Interfaces.IMultiFactorService
    {
        public long? FindMatchingTimeStep(string protectedSecret, string code) =>
            protectedSecret != "protected-secret" ? null : code switch { "123456" => 42, "654321" => 43, _ => null };
        public string CreateChallenge(Guid userId, bool rememberMe, PlatformProduct? product = null) => throw new NotSupportedException();
        public EduPlatform.Shared.Kernel.Results.Result<Identity.Application.Interfaces.MfaChallengePayload> ReadChallenge(string token) => throw new NotSupportedException();
        public Identity.Application.Interfaces.MfaSetupResponse CreateSetup(Guid userId, string email) => throw new NotSupportedException();
        public EduPlatform.Shared.Kernel.Results.Result<Identity.Application.Interfaces.MfaSetupPayload> ReadSetupToken(string token) => throw new NotSupportedException();
        public string ProtectSecret(string secret) => throw new NotSupportedException();
        public string UnprotectSecret(string protectedSecret) => throw new NotSupportedException();
        public IReadOnlyList<string> GenerateRecoveryCodes() => throw new NotSupportedException();
        public string HashRecoveryCode(string recoveryCode) => throw new NotSupportedException();
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
