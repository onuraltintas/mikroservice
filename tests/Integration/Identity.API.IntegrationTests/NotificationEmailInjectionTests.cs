using EduPlatform.Shared.Contracts.Events.Identity;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>
/// User-typed values (names, invitation messages, inviter address) must not be able to
/// add markup or links to e-mails sent from the platform's own address.
/// </summary>
public sealed class NotificationEmailInjectionTests
{
    private const string Payload = "<a href=\"https://evil.example/reset\">Şifreni yenile</a>";

    [Fact]
    public async Task Invitation_EncodesUserText_AndOnlyLinksToThePlatform()
    {
        var queue = await RunAsync<InvitationCreatedConsumer>(new InvitationCreatedEvent(
            Guid.NewGuid(), "teacher\"><script>x</script>@example.test", "student@example.test", null,
            "Student", Payload, "https://evil.example/accept", DateTime.UtcNow));

        var body = queue.Single().Body;
        body.Should().NotContain("<a href=\"https://evil.example", "the message is shown as text");
        body.Should().Contain("&lt;a href=&quot;https://evil.example/reset&quot;&gt;");
        body.Should().NotContain("<script>");
        body.Should().NotContain("href='https://evil.example/accept'", "foreign links are replaced");
        body.Should().Contain("href='https://app.example.test/auth/login'");
    }

    [Fact]
    public async Task Invitation_KeepsAPlatformLink()
    {
        var queue = await RunAsync<InvitationCreatedConsumer>(new InvitationCreatedEvent(
            Guid.NewGuid(), "teacher@example.test", "student@example.test", null,
            "Student", "Hoş geldin & başarılar", "https://speed.example.test/invitations/accept?id=1&x=2", DateTime.UtcNow));

        var body = queue.Single().Body;
        body.Should().Contain("href='https://speed.example.test/invitations/accept?id=1&amp;x=2'");
        body.Should().Contain("Hoş geldin &amp; başarılar");
    }

    // Roadmap Y6: a Coaching invitation is not branded as the speed-reading product.
    [Theory]
    [InlineData("StudentToTeacher", "öğrencisi olarak")]
    [InlineData("StudentToInstitution", "kurumuna öğrenci olarak")]
    [InlineData("TeacherToInstitution", "kurumuna öğretmen olarak")]
    public async Task CoachingInvitations_UseTheCoachingName(string type, string role)
    {
        var queue = await RunAsync<InvitationCreatedConsumer>(new InvitationCreatedEvent(
            Guid.NewGuid(), "koc@example.test", "student@example.test", null, type, null,
            "https://app.example.test/coaching-portal/profile", DateTime.UtcNow) { Product = "coaching" });

        var mail = queue.Single();
        mail.Subject.Should().Contain("Koçluk").And.NotContain("Hızlı Okuma");
        mail.Body.Should().Contain("Eduİvme Koçluk").And.Contain(role).And.NotContain("Hızlı Okuma");
        mail.Body.Should().Contain("href='https://app.example.test/coaching-portal/profile'");
    }

    // Review #2 (PR #111): a verification e-mail follows the product it was sent for; without a
    // product (older publishers) or with an unknown one, the role decides as before.
    // Second review H1: the link must open a page that exists. The Coaching site (admin-panel, PublicApp:BaseUrl)
    // verifies at /auth/confirm-email?userId=…&token=…; the speed-reading app at /auth/verify-email?token=…&userId=….
    [Theory]
    [InlineData("Student", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/confirm-email?userId={0}&token=token", "Eduİvme")]
    [InlineData("Teacher", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/confirm-email?userId={0}&token=token", "Eduİvme")]
    [InlineData("InstitutionAdmin", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/confirm-email?userId={0}&token=token", "Eduİvme")]
    [InlineData("Parent", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/confirm-email?userId={0}&token=token", "Eduİvme")]
    [InlineData("Coach", null, "Auth_VerifyEmail", "https://app.example.test/auth/confirm-email?userId={0}&token=token", "Eduİvme")]
    [InlineData("Student", "speed-reading", "SpeedReading_Auth_VerifyEmail", "https://speed.example.test/auth/verify-email?token=token&userId={0}", "Master Hızlı Okuma")]
    [InlineData("Student", null, "SpeedReading_Auth_VerifyEmail", "https://speed.example.test/auth/verify-email?token=token&userId={0}", "Master Hızlı Okuma")]
    [InlineData("Student", "unknown-product", "SpeedReading_Auth_VerifyEmail", "https://speed.example.test/auth/verify-email?token=token&userId={0}", "Master Hızlı Okuma")]
    public async Task VerificationEmail_UsesTheProductTemplateLinkAndName(string role, string? product, string template, string link, string appName)
    {
        foreach (var withTemplate in new[] { true, false })
        {
            var userId = Guid.NewGuid();
            var mail = (await RunAsync<UserRegisteredConsumer>(
                new UserRegisteredEvent(userId, "a@example.test", "Ada", "Kaya", "token", role) { Product = product },
                withTemplate ? (template, "Doğrula - Eduİvme", "<p>Eduİvme <a href='{{VerificationLink}}'>x</a></p>") : null)).Single();

            mail.Body.Should().Contain($"href='{string.Format(link, userId)}'");
            if (withTemplate) mail.Subject.Should().Be($"Doğrula - {appName}");
            else mail.Subject.Should().Contain(appName);
            mail.Body.Should().Contain(appName);
            if (appName == "Eduİvme") (mail.Subject + mail.Body).Should().NotContain("Hızlı Okuma");
        }
    }

    // Final review R3 and 1: end to end from the identity handler to the queued e-mail, with the production origins.
    private static void ProductionOrigins(PublicAppUrlOptions options)
    {
        options.BaseUrl = "https://onuraltintas.net";
        options.SpeedReadingBaseUrl = "https://masterhizliokuma.com";
        options.AdminPanelUrl = "https://eduivme.com";
    }

    private static Identity.Domain.Entities.User AccountIn(Identity.Domain.Enums.PlatformProduct product, bool confirmed)
    {
        var user = Identity.Domain.Entities.User.Create(Guid.NewGuid(), "hesap@example.test", "Ada", "Kaya");
        if (confirmed) user.ConfirmEmail();
        user.GrantProductAccess(product, Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var role = Identity.Domain.Entities.Role.Create("Student", "Student", true);
        var membership = new Identity.Domain.Entities.UserRole(user.Id, role.Id, product);
        typeof(Identity.Domain.Entities.UserRole).GetProperty("Role")!.SetValue(membership, role);
        user.AddRole(membership);
        return user;
    }

    [Theory]
    [InlineData(Identity.Domain.Enums.PlatformProduct.Coaching, "coaching", "SpeedReading_Auth_VerifyEmail", "https://onuraltintas.net/auth/confirm-email?userId={0}&token={1}", "Eduİvme")]
    [InlineData(Identity.Domain.Enums.PlatformProduct.SpeedReading, "speed-reading", "Auth_VerifyEmail", "https://masterhizliokuma.com/auth/verify-email?token={1}&userId={0}", "Master Hızlı Okuma")]
    public async Task ResentVerificationEmail_IsTheProductsEmailWithItsWorkingLink(
        Identity.Domain.Enums.PlatformProduct product, string requested, string otherTemplate, string link, string appName)
    {
        var user = AccountIn(product, confirmed: false);
        var events = new List<object>();
        var handler = new Identity.Application.Commands.ResendVerificationEmail.ResendVerificationEmailCommandHandler(
            Stub<Identity.Application.Interfaces.IUserRepository>((_, _) => Task.FromResult<Identity.Domain.Entities.User?>(user)),
            Stub<Identity.Application.Interfaces.IUnitOfWork>((_, _) => Task.FromResult(1)),
            Stub<IPublishEndpoint>((_, args) => { events.Add(args![0]!); return Task.CompletedTask; }));
        (await handler.Handle(new(user.Email, requested), default)).IsSuccess.Should().BeTrue();
        var registered = events.OfType<UserRegisteredEvent>().Single();

        // Seed the other product's template: it must not be chosen.
        var mail = (await RunAsync<UserRegisteredConsumer>(registered, (otherTemplate, "Yanlış", "<p>yanlış</p>"), ProductionOrigins)).Single();

        mail.Body.Should().Contain($"href='{string.Format(link, user.Id, user.EmailVerificationToken)}'");
        mail.Subject.Should().Contain(appName);
        mail.Body.Should().NotContain("yanlış");
        if (appName == "Eduİvme") (mail.Subject + mail.Body).Should().NotContain("Hızlı Okuma").And.NotContain("masterhizliokuma");
    }

    [Theory]
    [InlineData(Identity.Domain.Enums.PlatformProduct.Coaching, "coaching", "Auth_ForgotPassword", "https://onuraltintas.net/auth/reset-password?token=", "Eduİvme")]
    [InlineData(Identity.Domain.Enums.PlatformProduct.SpeedReading, "speed-reading", "SpeedReading_Auth_ForgotPassword", "https://masterhizliokuma.com/auth/reset-password?token=", "Master Hızlı Okuma")]
    public async Task ForgotPasswordEmail_IsTheProductsEmailWithItsWorkingLink(
        Identity.Domain.Enums.PlatformProduct product, string requested, string template, string link, string appName)
    {
        var user = AccountIn(product, confirmed: true);
        var events = new List<object>();
        var handler = new Identity.Application.Commands.ForgotPassword.ForgotPasswordCommandHandler(
            Stub<Identity.Application.Interfaces.IUserRepository>((_, _) => Task.FromResult<Identity.Domain.Entities.User?>(user)),
            Stub<Identity.Application.Interfaces.IUnitOfWork>((_, _) => Task.FromResult(1)),
            Stub<IPublishEndpoint>((_, args) => { events.Add(args![0]!); return Task.CompletedTask; }));
        (await handler.Handle(new(user.Email, requested), default)).IsSuccess.Should().BeTrue();
        var forgot = events.OfType<UserForgotPasswordEvent>().Single();

        foreach (var withTemplate in new[] { true, false })
        {
            var mail = (await RunAsync<UserForgotPasswordConsumer>(forgot,
                withTemplate ? (template, "Sıfırla - Eduİvme", "<p>Eduİvme <a href='{{ResetLink}}'>x</a></p>") : null, ProductionOrigins)).Single();
            mail.Body.Should().Contain($"href='{link}");
            mail.Body.Should().Contain("&email=hesap%40example.test'");
            mail.Body.Should().Contain(appName);
            if (appName == "Eduİvme") (mail.Subject + mail.Body).Should().NotContain("Hızlı Okuma").And.NotContain("masterhizliokuma");
        }
    }

    // Reviewer L1: a custom role next to a product role must not move the product's links to the admin panel.
    // The role named on the e-mail is the account's role in the e-mail's product; only an account without one
    // (custom role only, SystemAdmin) gets admin-panel links.
    private static Identity.Domain.Entities.User AccountWithRoles(Identity.Domain.Enums.PlatformProduct? access,
        params (string Name, Identity.Domain.Enums.PlatformProduct? Product)[] roles)
    {
        var user = Identity.Domain.Entities.User.Create(Guid.NewGuid(), "hesap@example.test", "Ada", "Kaya");
        if (access is { } product)
            user.GrantProductAccess(product, Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        foreach (var (name, product2) in roles)
        {
            var role = Identity.Domain.Entities.Role.Create(name, name, true);
            var membership = new Identity.Domain.Entities.UserRole(user.Id, role.Id, product2);
            typeof(Identity.Domain.Entities.UserRole).GetProperty("Role")!.SetValue(membership, role);
            user.AddRole(membership);
        }
        return user;
    }

    public static TheoryData<string, string> MixedRoleCases() => new()
    {
        { "coaching-student-and-moderator", "https://onuraltintas.net" },
        { "speed-reading-student-and-moderator", "https://masterhizliokuma.com" },
        { "coaching-moderator-only", "https://eduivme.com" },
        { "system-admin-only", "https://eduivme.com" },
    };

    private static (Identity.Domain.Entities.User User, string? Product) MixedRoleAccount(string scenario) => scenario switch
    {
        "coaching-student-and-moderator" => (AccountWithRoles(Identity.Domain.Enums.PlatformProduct.Coaching,
            ("Moderator", null), ("Student", Identity.Domain.Enums.PlatformProduct.Coaching)), "coaching"),
        "speed-reading-student-and-moderator" => (AccountWithRoles(Identity.Domain.Enums.PlatformProduct.SpeedReading,
            ("Moderator", null), ("Student", Identity.Domain.Enums.PlatformProduct.SpeedReading)), "speed-reading"),
        "coaching-moderator-only" => (AccountWithRoles(Identity.Domain.Enums.PlatformProduct.Coaching,
            ("Moderator", Identity.Domain.Enums.PlatformProduct.Coaching)), "coaching"),
        _ => (AccountWithRoles(null, ("SystemAdmin", null)), null),
    };

    private static async Task<List<object>> PublishFrom(Func<Identity.Application.Interfaces.IUserRepository, Identity.Application.Interfaces.IUnitOfWork, IPublishEndpoint, Task> run,
        Identity.Domain.Entities.User user)
    {
        var events = new List<object>();
        await run(Stub<Identity.Application.Interfaces.IUserRepository>((_, _) => Task.FromResult<Identity.Domain.Entities.User?>(user)),
            Stub<Identity.Application.Interfaces.IUnitOfWork>((_, _) => Task.FromResult(1)),
            Stub<IPublishEndpoint>((_, args) => { events.Add(args![0]!); return Task.CompletedTask; }));
        return events;
    }

    [Theory]
    [MemberData(nameof(MixedRoleCases))]
    public async Task PasswordReset_LinkFollowsTheProductRoleNotACustomRole(string scenario, string origin)
    {
        var (user, product) = MixedRoleAccount(scenario);
        user.ConfirmEmail();
        var events = await PublishFrom((users, uow, bus) => new Identity.Application.Commands.ForgotPassword.ForgotPasswordCommandHandler(users, uow, bus)
            .Handle(new(user.Email, product), default), user);
        var mail = (await RunAsync<UserForgotPasswordConsumer>(events.OfType<UserForgotPasswordEvent>().Single(), null, ProductionOrigins)).Single();
        mail.Body.Should().Contain($"href='{origin}/auth/reset-password?token=");
    }

    [Theory]
    [MemberData(nameof(MixedRoleCases))]
    public async Task ResentVerification_LinkFollowsTheProductRoleNotACustomRole(string scenario, string origin)
    {
        var (user, product) = MixedRoleAccount(scenario);
        var events = await PublishFrom((users, uow, bus) => new Identity.Application.Commands.ResendVerificationEmail.ResendVerificationEmailCommandHandler(users, uow, bus)
            .Handle(new(user.Email, product), default), user);
        var mail = (await RunAsync<UserRegisteredConsumer>(events.OfType<UserRegisteredEvent>().Single(), null, ProductionOrigins)).Single();
        mail.Body.Should().Contain($"href='{origin}/auth/");
        mail.Body.Should().NotContain(origin == "https://eduivme.com" ? "onuraltintas.net" : "eduivme.com");
    }

    [Theory]
    [MemberData(nameof(MixedRoleCases))]
    public async Task WelcomeAfterConfirmation_LoginLinkFollowsTheProductRoleNotACustomRole(string scenario, string origin)
    {
        var (user, product) = MixedRoleAccount(scenario);
        user.GenerateEmailVerificationToken();
        var token = user.EmailVerificationToken;
        var events = await PublishFrom((users, uow, bus) => new Identity.Application.Commands.ConfirmEmail.ConfirmEmailCommandHandler(users, uow, bus)
            .Handle(new(user.Id, token, product), default), user);
        var mail = (await RunAsync<UserEmailConfirmedConsumer>(events.OfType<UserEmailConfirmedEvent>().Single(), null, ProductionOrigins)).Single();
        mail.Body.Should().Contain($"href='{origin}/auth/login'");
    }

    public class StubProxy : System.Reflection.DispatchProxy
    {
        public Func<System.Reflection.MethodInfo, object?[]?, object?> Call = null!;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => Call(method!, args);
    }
    private static T Stub<T>(Func<System.Reflection.MethodInfo, object?[]?, object?> call) where T : class
    {
        var proxy = System.Reflection.DispatchProxy.Create<T, StubProxy>();
        ((StubProxy)(object)proxy).Call = call;
        return proxy;
    }

    // Third review N2: the welcome e-mail after verification follows the product; null keeps the role.
    [Theory]
    [InlineData("Student", "coaching", "Auth_Welcome", "https://app.example.test/auth/login", "Eduİvme")]
    [InlineData("Parent", "coaching", "Auth_Welcome", "https://app.example.test/auth/login", "Eduİvme")]
    [InlineData("Student", "speed-reading", "SpeedReading_Auth_Welcome", "https://speed.example.test/auth/login", "Master Hızlı Okuma")]
    [InlineData("Student", null, "SpeedReading_Auth_Welcome", "https://speed.example.test/auth/login", "Master Hızlı Okuma")]
    public async Task WelcomeEmail_UsesTheProductTemplateLoginLinkAndName(string role, string? product, string template, string login, string appName)
    {
        foreach (var withTemplate in new[] { true, false })
        {
            var mail = (await RunAsync<UserEmailConfirmedConsumer>(
                new UserEmailConfirmedEvent(Guid.NewGuid(), "a@example.test", "Ada", "Kaya", role) { Product = product },
                withTemplate ? (template, "Hoş geldin - Eduİvme", "<p>Eduİvme <a href='{{LoginLink}}'>x</a></p>") : null)).Single();

            mail.Body.Should().Contain($"href='{login}'");
            mail.Body.Should().Contain(appName);
            if (withTemplate) mail.Subject.Should().Be($"Hoş geldin - {appName}");
            if (appName == "Eduİvme") (mail.Subject + mail.Body).Should().NotContain("Hızlı Okuma");
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuthEmails_EncodeNamesInTemplatesAndFallbacks(bool withTemplate)
    {
        var registered = await RunAsync<UserRegisteredConsumer>(
            new UserRegisteredEvent(Guid.NewGuid(), "a@example.test", Payload, Payload, "token", "Coach"),
            withTemplate ? ("Auth_VerifyEmail", "Doğrula {{FirstName}}", "<p>{{FirstName}} {{LastName}} <a href='{{VerificationLink}}'>x</a></p>") : null);
        var forgot = await RunAsync<UserForgotPasswordConsumer>(
            new UserForgotPasswordEvent(Guid.NewGuid(), "a@example.test", Payload, Payload, "token", "Coach"),
            withTemplate ? ("Auth_ForgotPassword", "Sıfırla {{FirstName}}", "<p>{{FirstName}} {{LastName}} {{ResetLink}}</p>") : null);
        var confirmed = await RunAsync<UserEmailConfirmedConsumer>(
            new UserEmailConfirmedEvent(Guid.NewGuid(), "a@example.test", Payload, Payload, "Coach"),
            withTemplate ? ("Auth_Welcome", "Hoş geldin {{FirstName}}", "<p>{{FirstName}} {{LastName}} {{Email}}</p>") : null);
        var created = await RunAsync<UserCreatedConsumer>(
            new UserCreatedEvent(Guid.NewGuid(), "a@example.test", Payload, Payload, "Coach", "token",
                DateTime.UtcNow.AddHours(2), DateTime.UtcNow) { Product = "coaching" },
            withTemplate ? ("Auth_DirectCreate", "Hesap {{FirstName}}", "<p>{{FirstName}} {{LastName}} {{Role}} {{PasswordSetupUrl}}</p>") : null);

        foreach (var message in registered.Concat(forgot).Concat(confirmed).Concat(created))
        {
            message.Body.Should().NotContain("<a href=\"https://evil.example", $"{message.ConsumerType} must encode names");
            message.Body.Should().Contain("&lt;a href=");
            message.Subject.Should().NotContainAny("\r", "\n");
        }
    }

    [Theory]
    [InlineData("https://app.example.test/x", "https://app.example.test/x")]
    [InlineData("https://speed.example.test/y?a=1", "https://speed.example.test/y?a=1")]
    [InlineData("https://app.example.test.evil.example/x", "fallback")]
    [InlineData("javascript:alert(1)", "fallback")]
    [InlineData("https://user@app.example.test/x", "fallback")]
    [InlineData(null, "fallback")]
    public void PlatformLink_AcceptsOnlyThePlatformOrigins(string? link, string expected)
    {
        var origins = new PublicAppUrlOptions { BaseUrl = "https://app.example.test", SpeedReadingBaseUrl = "https://speed.example.test" };

        EmailContent.PlatformLink(link, origins, "fallback").Should().Be(expected);
    }

    [Fact]
    public void SubjectText_RemovesLineBreaks()
    {
        EmailContent.SubjectText("Ada\r\nBcc: someone@example.test").Should().Be("Ada  Bcc: someone@example.test");
    }

    private static async Task<IReadOnlyList<QueuedMessage>> RunAsync<TConsumer>(
        object message,
        (string Name, string Subject, string Body)? template = null,
        Action<PublicAppUrlOptions>? origins = null)
        where TConsumer : class, IConsumer
    {
        var queue = new RecordingEmailQueue();
        var services = new ServiceCollection();
        // One database for the whole run: the options lambda runs per scope, so a name created inside it
        // would give the consumer an empty database and the seeded template would never be used.
        var databaseName = Guid.NewGuid().ToString();
        services.AddDbContext<NotificationDbContext>(options => options.UseInMemoryDatabase(databaseName));
        services.AddScoped<INotificationDbContext>(provider => provider.GetRequiredService<NotificationDbContext>());
        services.AddSingleton<IEmailDeliveryQueue>(queue);
        services.AddSingleton<INotificationService>(new NoopNotificationService());
        services.Configure<PublicAppUrlOptions>(options =>
        {
            options.BaseUrl = "https://app.example.test";
            options.SpeedReadingBaseUrl = "https://speed.example.test";
            origins?.Invoke(options);
        });
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.UseSuiteTimeouts();
            configurator.AddConsumer<TConsumer>();
            configurator.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        if (template is { } value)
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            db.EmailTemplates.Add(EmailTemplate.Create(value.Name, "Auth", value.Subject, value.Body));
            await db.SaveChangesAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(message, message.GetType());
            await TestHarnessTimeouts.UntilAsync(() => !queue.Messages.IsEmpty);
            queue.Messages.Should().ContainSingle($"{typeof(TConsumer).Name} queues one e-mail");
            return queue.Messages.ToArray();
        }
        finally
        {
            await harness.Stop();
        }
    }

    private sealed record QueuedMessage(string ConsumerType, string Subject, string Body);

    private sealed class RecordingEmailQueue : IEmailDeliveryQueue
    {
        public System.Collections.Concurrent.ConcurrentQueue<QueuedMessage> Messages { get; } = new();

        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body,
            Guid? subjectUserId = null, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(new QueuedMessage(consumerType, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed class NoopNotificationService : INotificationService
    {
        public Task SendNotificationAsync(Guid userId, string title, string message, string type,
            string? relatedEntityId = null, Guid? sourceMessageId = null) => Task.CompletedTask;
    }
}
