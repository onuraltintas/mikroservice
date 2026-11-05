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
    // product (older publishers) the role decides as before.
    [Theory]
    [InlineData("Student", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/verify-email", "Eduİvme")]
    [InlineData("Teacher", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/verify-email", "Eduİvme")]
    [InlineData("InstitutionAdmin", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/verify-email", "Eduİvme")]
    [InlineData("Parent", "coaching", "Auth_VerifyEmail", "https://app.example.test/auth/verify-email", "Eduİvme")]
    [InlineData("Student", "speed-reading", "SpeedReading_Auth_VerifyEmail", "https://speed.example.test/auth/verify-email", "Master Hızlı Okuma")]
    [InlineData("Student", null, "SpeedReading_Auth_VerifyEmail", "https://speed.example.test/auth/verify-email", "Master Hızlı Okuma")]
    public async Task VerificationEmail_UsesTheProductTemplateLinkAndName(string role, string? product, string template, string link, string appName)
    {
        foreach (var withTemplate in new[] { true, false })
        {
            var mail = (await RunAsync<UserRegisteredConsumer>(
                new UserRegisteredEvent(Guid.NewGuid(), "a@example.test", "Ada", "Kaya", "token", role) { Product = product },
                withTemplate ? (template, "Doğrula - Eduİvme", "<p>Eduİvme <a href='{{VerificationLink}}'>x</a></p>") : null)).Single();

            mail.Body.Should().Contain($"href='{link}?token=token&userId=");
            if (withTemplate) mail.Subject.Should().Be($"Doğrula - {appName}");
            else mail.Subject.Should().Contain(appName);
            mail.Body.Should().Contain(appName);
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
        (string Name, string Subject, string Body)? template = null)
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
