using EduPlatform.Shared.Contracts.Events.Identity;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

public sealed class UserCreatedConsumerTests
{
    [Theory]
    [InlineData("Student", "coaching", false)]
    [InlineData("Teacher", "coaching", false)]
    [InlineData("InstitutionAdmin", "coaching", false)]
    [InlineData("InstitutionOwner", "coaching", false)]
    [InlineData("Student", "speed-reading", true)]
    [InlineData("Teacher", "speed-reading", true)]
    [InlineData("InstitutionAdmin", "speed-reading", true)]
    [InlineData("InstitutionOwner", "speed-reading", true)]
    [InlineData("ContentReviewer", "speed-reading", true)]
    [InlineData("Student", null, true)]
    [InlineData("SystemAdmin", null, false)]
    public async Task UserCreatedConsumer_ShouldQueueAProductSetupLinkWithoutAPlaintextPassword(string role, string? product, bool speedReading)
    {
        var queue = new RecordingEmailQueue();
        var notificationService = new NoopNotificationService();
        var services = new ServiceCollection();

        services.AddDbContext<NotificationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddScoped<INotificationDbContext>(provider =>
            provider.GetRequiredService<NotificationDbContext>());
        services.AddSingleton<IEmailDeliveryQueue>(queue);
        services.AddSingleton<INotificationService>(notificationService);
        services.Configure<Notification.Application.Configuration.PublicAppUrlOptions>(options =>
        {
            options.BaseUrl = "https://app.example.test";
            options.SpeedReadingBaseUrl = "https://speed.example.test";
        });
        services.AddMassTransitTestHarness(configurator =>
        {
            configurator.UseSuiteTimeouts();
            configurator.AddConsumer<UserCreatedConsumer>();
            configurator.UsingInMemory((context, busConfigurator) =>
                busConfigurator.ConfigureEndpoints(context));
        });

        await using var provider = services.BuildServiceProvider();
        await using (var db = provider.GetRequiredService<NotificationDbContext>())
        {
            db.EmailTemplates.Add(EmailTemplate.Create(
                "Auth_DirectCreate",
                "Auth",
                "Eduİvme hesabınız hazır",
                "Eduİvme: Merhaba {{FirstName}}, <a href=\"{{PasswordSetupUrl}}\">Parolanızı belirleyin</a>"));
            db.EmailTemplates.Add(EmailTemplate.Create(
                "SpeedReading_Auth_DirectCreate", "Auth", "Eduİvme hesabınız hazır",
                "Eduİvme: Merhaba {{FirstName}}, <a href=\"{{PasswordSetupUrl}}\">Parolanızı belirleyin</a>"));
            await db.SaveChangesAsync();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            await harness.Bus.Publish(new UserCreatedEvent(
                Guid.NewGuid(),
                "student@example.test",
                "Ada",
                "Lovelace",
                role,
                "one-time-setup-token",
                DateTime.UtcNow.AddHours(2),
                DateTime.UtcNow) { Product = product });

            (await harness.Consumed.Any<UserCreatedEvent>()).Should().BeTrue();
            await TestHarnessTimeouts.UntilAsync(() => queue.Messages.Count == 1);

            var body = queue.Messages.Single().Body;
            body.Should().Contain("/auth/reset-password?");
            body.Should().Contain("token=one-time-setup-token");
            body.Should().Contain("email=student%40example.test");
            body.Should().Contain(speedReading ? "https://speed.example.test/auth/" : "https://app.example.test/auth/");
            body.Should().Contain(speedReading ? "Master Hızlı Okuma" : "Eduİvme");
            queue.Messages.Single().Subject.Should().Contain(speedReading ? "Master Hızlı Okuma" : "Eduİvme");
            body.Should().NotContain("TemporaryPassword");
            body.Should().NotContain("Pass:");
        }
        finally
        {
            await harness.Stop();
        }
    }


    private sealed class RecordingEmailQueue : IEmailDeliveryQueue
    {
        public List<QueuedMessage> Messages { get; } = [];

        public Task QueueAsync(
            Guid messageId,
            string consumerType,
            string recipient,
            string subject,
            string body,
            Guid? subjectUserId = null,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new QueuedMessage(messageId, consumerType, recipient, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed record QueuedMessage(
        Guid MessageId,
        string ConsumerType,
        string Recipient,
        string Subject,
        string Body);

    private sealed class NoopNotificationService : INotificationService
    {
        public Task SendNotificationAsync(
            Guid userId,
            string title,
            string message,
            string type,
            string? relatedEntityId = null,
            Guid? sourceMessageId = null)
            => Task.CompletedTask;
    }
}
