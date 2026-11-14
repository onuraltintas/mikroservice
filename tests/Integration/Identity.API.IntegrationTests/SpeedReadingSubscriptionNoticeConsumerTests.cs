using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.SpeedReading;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Hızlı Okuma billing parity (14.11.2026): the payer is e-mailed about payment decisions, withdrawal answers and expiry
/// with a link to the subscription page; the admin is e-mailed about new payment notices and withdrawal requests.
/// </summary>
public sealed class SpeedReadingSubscriptionNoticeConsumerTests
{
    [Fact]
    public async Task PayerNotices_AreEmailedWithALinkToTheSubscriptionPage_AndAdminNoticesGoToTheAlertAddress()
    {
        var payer = Guid.NewGuid();
        var (provider, mail) = await StartAsync("finans@example.test");
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.TransferApproved, payer, "veli@example.test", new DateTime(2027, 11, 20, 21, 30, 0, DateTimeKind.Utc)));
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.WithdrawalRejected, payer, "veli@example.test", detail: "Süre doldu"));
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.TransferSubmitted, null, null, detail: "EFT-1"));
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.WithdrawalRequested, null, null, detail: "EFT-1"));
            // A payer without an e-mail address gets only the in-app notification the Speed Reading service wrote.
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.Expired, payer, null, new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc)));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 4);
            await Task.Delay(300);

            mail.Messages.Should().HaveCount(4);
            var approved = mail.Messages.Single(x => x.Subject.Contains("onaylandı") && x.Recipient == "veli@example.test" && x.Subject.Contains("Ödemeniz"));
            approved.Subject.Should().StartWith("Master Hızlı Okuma: ");
            approved.Body.Should().Contain("https://hizliokuma.example.test/student/subscription").And.Contain("Yıllık plan").And.Contain("21.11.2027 00:30");
            approved.SubjectUserId.Should().Be(payer);
            mail.Messages.Single(x => x.Subject.Contains("Cayma talebiniz onaylanmadı")).Body.Should().Contain("re doldu");
            var admin = mail.Messages.Where(x => x.Recipient == "finans@example.test").ToArray();
            admin.Should().HaveCount(2);
            admin.Should().OnlyContain(x => x.Body.Contains("https://yonetim.example.test/dashboard/speed-reading/subscriptions") && x.Body.Contains("EFT-1") && x.SubjectUserId == null);
            admin.Select(x => x.ConsumerType).Should().OnlyContain(x => x == "SpeedReadingSubscriptionNoticeConsumer.Admin");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task AdminNotices_AreSkippedWithoutAnAlertAddress()
    {
        var (provider, mail) = await StartAsync(null);
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.TransferSubmitted, null, null, detail: "EFT-1"));
            await harness.Bus.Publish(Notice(SpeedReadingSubscriptionNoticeKinds.Expiring, Guid.NewGuid(), "veli@example.test", new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc)));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 1);
            await Task.Delay(300);

            mail.Messages.Should().ContainSingle().Which.Subject.Should().Contain("yakında sona eriyor");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public void EveryKindHasWords()
    {
        foreach (var kind in new[]
                 {
                     SpeedReadingSubscriptionNoticeKinds.TransferSubmitted, SpeedReadingSubscriptionNoticeKinds.TransferApproved,
                     SpeedReadingSubscriptionNoticeKinds.TransferRejected, SpeedReadingSubscriptionNoticeKinds.WithdrawalRequested,
                     SpeedReadingSubscriptionNoticeKinds.WithdrawalApproved, SpeedReadingSubscriptionNoticeKinds.WithdrawalRejected,
                     SpeedReadingSubscriptionNoticeKinds.Expiring, SpeedReadingSubscriptionNoticeKinds.Expired
                 })
        {
            var text = SpeedReadingSubscriptionNoticeTexts.Describe(kind, "Yıllık plan", "01.12.2026 12:00", "ayrıntı");
            text.Should().NotBeNull(kind);
            text!.Title.Length.Should().BeInRange(5, 200);
            text.Message.Should().Contain("Yıllık plan");
        }
        SpeedReadingSubscriptionNoticeTexts.Describe("Unknown", "p", null, null).Should().BeNull();
        SpeedReadingSubscriptionNoticeKinds.IsForAdmin(SpeedReadingSubscriptionNoticeKinds.TransferSubmitted).Should().BeTrue();
        SpeedReadingSubscriptionNoticeKinds.IsForAdmin(SpeedReadingSubscriptionNoticeKinds.TransferApproved).Should().BeFalse();
    }

    private static SpeedReadingSubscriptionNoticeV1 Notice(string kind, Guid? user, string? email, DateTime? endsAt = null, string? detail = null) =>
        new(Guid.NewGuid(), user, email, kind, "Yıllık plan", endsAt, detail, DateTime.UtcNow);

    private static async Task<(ServiceProvider, RecordingEmailQueue)> StartAsync(string? adminEmail)
    {
        var mail = new RecordingEmailQueue();
        var services = new ServiceCollection();
        services.AddSingleton<IEmailDeliveryQueue>(mail);
        services.Configure<PublicAppUrlOptions>(options =>
        {
            options.BaseUrl = "https://app.example.test";
            options.SpeedReadingBaseUrl = "https://hizliokuma.example.test";
            options.AdminPanelUrl = "https://yonetim.example.test";
        });
        services.Configure<CoachingNoticeOptions>(options => options.AdminAlertEmail = adminEmail);
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<SpeedReadingSubscriptionNoticeConsumer>();
            x.UsingInMemory((context, config) => config.ConfigureEndpoints(context));
        });
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITestHarness>().Start();
        return (provider, mail);
    }

    private sealed class RecordingEmailQueue : IEmailDeliveryQueue
    {
        public ConcurrentQueue<QueuedMessage> Messages { get; } = new();
        public Task QueueAsync(Guid messageId, string consumerType, string recipient, string subject, string body,
            Guid? subjectUserId = null, CancellationToken cancellationToken = default)
        {
            Messages.Enqueue(new QueuedMessage(messageId, consumerType, recipient, subject, body, subjectUserId));
            return Task.CompletedTask;
        }
    }

    private sealed record QueuedMessage(Guid MessageId, string ConsumerType, string Recipient, string Subject, string Body, Guid? SubjectUserId);
}
