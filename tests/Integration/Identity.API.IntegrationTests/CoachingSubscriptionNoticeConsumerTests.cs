using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

public sealed class CoachingSubscriptionNoticeConsumerTests
{
    [Fact]
    public async Task PayerNotices_ArriveInAppAndByEmailWithALinkToTheRightPortal()
    {
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
        var (provider, dispatcher, mail) = await StartAsync("finans@example.test");
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            var approved = Notice(CoachingSubscriptionNoticeKinds.TransferApproved, student, "ada@example.test", "Individual");
            await harness.Bus.Publish(approved);
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.Expiring, teacher, "ogretmen@example.test", "Teacher"));
            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 2 && mail.Messages.Count >= 2);

            var inApp = dispatcher.Calls.Single(x => x.RecipientIds.SequenceEqual([student]));
            (inApp.EventMessageId, inApp.Type, inApp.RelatedEntityId)
                .Should().Be((approved.NoticeId, "SubscriptionTransferApproved", approved.NoticeId.ToString()));
            inApp.Title.Should().Contain("onaylandı");
            var studentMail = mail.Messages.Single(x => x.Recipient == "ada@example.test");
            studentMail.Body.Should().Contain("https://app.example.test/coaching-portal/subscription").And.Contain("Bireysel aylık");
            studentMail.SubjectUserId.Should().Be(student);
            var teacherMail = mail.Messages.Single(x => x.Recipient == "ogretmen@example.test");
            teacherMail.Subject.Should().Contain("sona eriyor");
            // Coaching audit K3: teachers manage billing in the coaching staff portal, not on the speed-reading login.
            teacherMail.Body.Should().Contain("https://app.example.test/staff/?product=coaching").And.NotContain("coaching-portal");
            dispatcher.Calls.Single(x => x.RecipientIds.SequenceEqual([teacher])).Type.Should().Be("SubscriptionExpiring");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task AdminNotices_GoOnlyToTheConfiguredAlertAddress()
    {
        var (provider, dispatcher, mail) = await StartAsync("finans@example.test");
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.TransferSubmitted, null, null, "Individual",
                "Ada <b>Veli</b> · EFT-1"));
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.InstitutionExpired, null, "kurum@example.test", "Institution"));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 3);

            dispatcher.Calls.Should().BeEmpty("there is no in-app recipient for platform or institution notices");
            var submitted = mail.Messages.Single(x => x.Subject.Contains("havale", StringComparison.OrdinalIgnoreCase));
            submitted.Recipient.Should().Be("finans@example.test");
            submitted.Body.Should().Contain("https://app.example.test/dashboard/coaching/subscriptions")
                .And.Contain("Ada &lt;b&gt;Veli&lt;/b&gt;").And.NotContain("<b>Veli</b>");
            mail.Messages.Where(x => x.Subject.Contains("Kurum", StringComparison.Ordinal)).Select(x => x.Recipient)
                .Should().BeEquivalentTo(["finans@example.test", "kurum@example.test"]);
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task ExpiredTransferNotices_TellThePayerToSendANewOne()
    {
        var student = Guid.NewGuid();
        var (provider, dispatcher, mail) = await StartAsync("finans@example.test");
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.TransferExpired, student, "ada@example.test", "Individual", "EFT-STALE-001"));
            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 1 && mail.Messages.Count >= 1);
            var call = dispatcher.Calls.Single();
            call.Type.Should().Be("SubscriptionTransferExpired");
            (call.Title + " " + call.Message).Should().Contain("EFT-STALE-001").And.Contain("yeni");
            mail.Messages.Should().ContainSingle(x => x.Recipient == "ada@example.test");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    // Roadmap Y7: the parent's weekly summary arrives by e-mail and in the app, with a link to the parent panel.
    [Fact]
    public async Task ParentWeeklySummary_IsEmailedAndShownToTheParent()
    {
        var parent = Guid.NewGuid(); var student = Guid.NewGuid();
        var (provider, dispatcher, mail) = await StartAsync("finans@example.test");
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            var summary = new CoachingParentWeeklySummaryV1(Guid.NewGuid(), parent, "veli@example.test", "Ayşe <b>Veli</b>", student, "Ada Öğrenci",
                new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), 5, 4, 2, 1, 1, 1, 1, 2, "TYT Deneme 3", 87.5m);
            await harness.Bus.Publish(summary);
            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 1 && mail.Messages.Count >= 1);
            var inApp = dispatcher.Calls.Single();
            (inApp.RecipientIds.Single(), inApp.Type, inApp.RelatedEntityId).Should().Be((parent, "ParentWeeklySummary", student.ToString()));
            var message = mail.Messages.Single();
            message.Recipient.Should().Be("veli@example.test");
            message.Subject.Should().Contain("Ada Öğrenci").And.Contain("haftalık");
            message.Body.Should().Contain("5 çalışmanın 4").And.Contain("TYT Deneme 3").And.Contain("87,5")
                .And.Contain("https://app.example.test/coaching-portal/parent").And.NotContain("<b>Veli</b>");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task AdminNotices_AreSkippedWhenNoAlertAddressIsConfigured()
    {
        var (provider, dispatcher, mail) = await StartAsync(null);
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Notice(CoachingSubscriptionNoticeKinds.TransferSubmitted, null, null, "Individual", "Ada · EFT-1"));
            (await harness.Consumed.Any<CoachingSubscriptionNoticeV1>(x => x.Exception is null)).Should().BeTrue();
            mail.Messages.Should().BeEmpty(); dispatcher.Calls.Should().BeEmpty();
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    private static CoachingSubscriptionNoticeV1 Notice(string kind, Guid? user, string? email, string audience, string? detail = null) =>
        new(Guid.NewGuid(), user, email, kind, audience, audience == "Institution" ? "Kurum yıllık" : "Bireysel aylık",
            new DateTime(2026, 11, 3, 9, 0, 0, DateTimeKind.Utc), detail, DateTime.UtcNow);

    private static async Task<(ServiceProvider, RecordingDispatcher, RecordingEmailQueue)> StartAsync(string? adminEmail)
    {
        var dispatcher = new RecordingDispatcher(); var mail = new RecordingEmailQueue();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingNotificationDispatcher>(dispatcher);
        services.AddSingleton<IEmailDeliveryQueue>(mail);
        services.Configure<PublicAppUrlOptions>(options => options.BaseUrl = "https://app.example.test");
        services.Configure<CoachingNoticeOptions>(options => options.AdminAlertEmail = adminEmail);
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<CoachingSubscriptionNoticeConsumer>();
            x.AddConsumer<CoachingParentWeeklySummaryConsumer>();
            x.UsingInMemory((context, config) => config.ConfigureEndpoints(context));
        });
        var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<ITestHarness>().Start();
        return (provider, dispatcher, mail);
    }

    private sealed class RecordingDispatcher : ICoachingNotificationDispatcher
    {
        public ConcurrentQueue<DispatchCall> Calls { get; } = new();
        public Task SendAsync(Guid eventMessageId, IReadOnlyCollection<Guid> recipientIds, string title, string message,
            string type, string relatedEntityId, CancellationToken cancellationToken)
        {
            Calls.Enqueue(new DispatchCall(eventMessageId, recipientIds.ToArray(), title, message, type, relatedEntityId));
            return Task.CompletedTask;
        }
    }

    private sealed record DispatchCall(Guid EventMessageId, Guid[] RecipientIds, string Title, string Message, string Type, string RelatedEntityId);

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
