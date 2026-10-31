using System.Collections.Concurrent;
using EduPlatform.Shared.Contracts.Events.Identity;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Notification.API.Messaging;
using Notification.Application.Configuration;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;

namespace Identity.API.IntegrationTests;

// V1 (parent panel): each step of a parent link reaches the other side in the app and by e-mail, with a link to act on it.
public sealed class ParentLinkNotificationConsumerTests
{
    [Fact]
    public async Task ARequestReachesTheStudentInTheAppAndByEmail()
    {
        var (provider, dispatcher, mail) = await StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            var message = Message(ParentLinkChanges.Requested, "Student", "ada@example.test", "Ada", "Ayşe <b>Veli</b>", "coaching",
                "/coaching-portal/account/settings/profile");
            await harness.Bus.Publish(message);
            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 1 && mail.Messages.Count >= 1);

            var inApp = dispatcher.Calls.Single();
            (inApp.EventMessageId, inApp.RecipientIds.Single(), inApp.Type, inApp.RelatedEntityId)
                .Should().Be((message.EventId, message.RecipientUserId, "ParentLinkRequested", message.LinkId.ToString()));
            inApp.Title.Should().Be("Velin bağlantı isteği gönderdi");
            inApp.Message.Should().Contain("Ayşe <b>Veli</b>").And.Contain("onay");
            var email = mail.Messages.Single();
            (email.Recipient, email.SubjectUserId).Should().Be(("ada@example.test", message.RecipientUserId));
            email.Subject.Should().Be("Eduİvme Koçluk: Velin bağlantı isteği gönderdi");
            email.Body.Should().Contain("Merhaba Ada,").And.Contain("Ayşe &lt;b&gt;Veli&lt;/b&gt;").And.NotContain("<b>Veli</b>")
                .And.Contain("href=\"https://app.example.test/coaching-portal/account/settings/profile\"");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task ASpeedReadingStudentIsSentToTheSpeedReadingSite()
    {
        var (provider, dispatcher, mail) = await StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            await harness.Bus.Publish(Message(ParentLinkChanges.Requested, "Student", "ada@example.test", "Ada", "Ayşe Veli", "speed-reading", "/veli-baglantisi"));
            await TestHarnessTimeouts.UntilAsync(() => mail.Messages.Count >= 1);
            var email = mail.Messages.Single();
            email.Subject.Should().StartWith("Master Hızlı Okuma:");
            email.Body.Should().Contain("href=\"https://hizliokuma.example.test/veli-baglantisi\"").And.Contain("Master Hızlı Okuma");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public async Task ApprovalAndRemovalReachTheOtherSide()
    {
        var (provider, dispatcher, mail) = await StartAsync();
        var harness = provider.GetRequiredService<ITestHarness>();
        try
        {
            var approved = Message(ParentLinkChanges.Approved, "Parent", "veli@example.test", "Ayşe", "Ada Öğrenci", "coaching", "/coaching-portal/parent");
            var removed = Message(ParentLinkChanges.Revoked, "Parent", null, "Ayşe", "Ada Öğrenci", "coaching", "/coaching-portal/parent");
            await harness.Bus.Publish(approved);
            await harness.Bus.Publish(removed);
            await TestHarnessTimeouts.UntilAsync(() => dispatcher.Calls.Count >= 2 && mail.Messages.Count >= 1);

            var approval = dispatcher.Calls.Single(x => x.EventMessageId == approved.EventId);
            approval.Type.Should().Be("ParentLinkApproved");
            approval.Message.Should().Contain("Ada Öğrenci");
            mail.Messages.Single().Body.Should().Contain("href=\"https://app.example.test/coaching-portal/parent\"");
            var removal = dispatcher.Calls.Single(x => x.EventMessageId == removed.EventId);
            removal.Type.Should().Be("ParentLinkRevoked");
            removal.Message.Should().Contain("Ada Öğrenci");
            mail.Messages.Should().ContainSingle("there is no address for the removal notice");
        }
        finally { await harness.Stop(); await provider.DisposeAsync(); }
    }

    [Fact]
    public void TheConsumerHasItsOwnProductionQueue() =>
        NotificationReceiveEndpoints.All.Should().ContainSingle(x => x.Consumer == typeof(ParentLinkNotificationConsumer))
            .Which.Queue.Should().Be("identity-parent-link-changed");

    private static ParentLinkNotificationV1 Message(string change, string role, string? email, string firstName, string other, string product, string path) =>
        new(Guid.NewGuid(), Guid.NewGuid(), change, Guid.NewGuid(), role, email, firstName, other, product, path, DateTime.UtcNow);

    private static async Task<(ServiceProvider, RecordingDispatcher, RecordingEmailQueue)> StartAsync()
    {
        var dispatcher = new RecordingDispatcher(); var mail = new RecordingEmailQueue();
        var services = new ServiceCollection();
        services.AddSingleton<ICoachingNotificationDispatcher>(dispatcher);
        services.AddSingleton<IEmailDeliveryQueue>(mail);
        services.Configure<PublicAppUrlOptions>(options =>
        {
            options.BaseUrl = "https://app.example.test";
            options.SpeedReadingBaseUrl = "https://hizliokuma.example.test";
        });
        services.AddMassTransitTestHarness(x =>
        {
            x.UseSuiteTimeouts();
            x.AddConsumer<ParentLinkNotificationConsumer>();
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
