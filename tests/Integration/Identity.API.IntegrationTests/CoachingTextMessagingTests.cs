using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Services;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Identity.API.IntegrationTests;

// Roadmap O9: SMS and WhatsApp channels without a provider yet. Users opt in per category; while sending is
// switched off nothing leaves the system and only a minimal, masked record is kept. Commercial-message consent is
// stored for the İleti Yönetim Sistemi (İYS) and waits for the integration.
public sealed class CoachingTextMessagingTests
{
    private static readonly Guid Parent = Guid.NewGuid(), NoPhone = Guid.NewGuid();

    private sealed class Fixture : IAsyncDisposable
    {
        public NotificationDbContext Db { get; } = new(new DbContextOptionsBuilder<NotificationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public CoachingNotificationPreferencesTests.RecordingNotifications App { get; } = new();
        public CoachingNotificationPreferencesTests.RecordingMail Mail { get; } = new();
        public CoachingNotificationPreferencesTests.StubDirectory Directory { get; } = new();
        public TextMessagingOptions Text { get; } = new();
        public CoachingNotificationDispatcher Dispatcher { get; }
        public NotificationPreferenceService Preferences { get; }
        public Fixture()
        {
            Directory.Contacts[Parent] = new RecipientContact(Parent, "veli@example.test", "Ayşe", "Yılmaz") { Phone = "+905321234567" };
            Directory.Contacts[NoPhone] = new RecipientContact(NoPhone, "can@example.test", "Can", "Demir");
            var queue = new TextMessageQueue(Db, Options.Create(Text));
            var policy = new NotificationChannelPolicy(Db, Directory, Mail, Options.Create(new PublicAppUrlOptions { BaseUrl = "https://onuraltintas.net" }), queue);
            Dispatcher = new CoachingNotificationDispatcher(App, policy);
            Preferences = new NotificationPreferenceService(Db);
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    [Fact]
    public async Task OptedInRemindersAreRecordedMaskedAndNotSentWhileTextMessagingIsOff()
    {
        await using var f = new Fixture();
        await f.Preferences.SaveAsync(Parent, [new("Sessions", true, true) { Sms = true, WhatsApp = true }], default);
        await f.Preferences.SaveAsync(NoPhone, [new("Sessions", true, true) { Sms = true }], default);
        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Parent, NoPhone], "Yaklaşan koçluk seansı", "Seansın bir saat içinde başlayacak.", "SessionReminder", "s1", default);

        var rows = await f.Db.TextMessageDeliveries.AsNoTracking().ToListAsync();
        rows.Select(x => x.Channel).Should().BeEquivalentTo(new[] { "Sms", "WhatsApp" });
        rows.Should().OnlyContain(x => x.Status == TextMessageStatus.Suppressed && x.Body == "" && x.Recipient == "+90********67" && x.SubjectUserId == Parent);

        // Only reminders go by text; a newly planned session does not.
        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Parent], "Yeni koçluk seansı", "Planlandı", "SessionScheduled", "s2", default);
        (await f.Db.TextMessageDeliveries.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task WhenSwitchedOnTextsWaitForTheProviderAndARedeliveredEventIsQueuedOnce()
    {
        await using var f = new Fixture(); f.Text.Enabled = true; f.Text.Provider = "Netgsm";
        await f.Preferences.SaveAsync(Parent, [new("ParentSummary", true, true) { Sms = true }], default);
        var eventId = Guid.NewGuid();
        for (var i = 0; i < 2; i++)
            await f.Dispatcher.SendAsync(eventId, [Parent], "Ada için haftalık özet", "Planlanan 5 çalışmanın 4 tanesi tamamlandı.", "ParentWeeklySummary", "child", default);
        var row = (await f.Db.TextMessageDeliveries.AsNoTracking().ToListAsync()).Should().ContainSingle().Subject;
        row.Status.Should().Be(TextMessageStatus.Pending);
        row.Recipient.Should().Be("+905321234567");
        row.Body.Should().StartWith("Eduİvme: Ada için haftalık özet"); row.Body.Length.Should().BeLessThanOrEqualTo(TextMessageQueue.MaxBodyLength);
    }

    [Fact]
    public async Task TextChoicesAreOffByDefaultAndOnlyWhereTextMakesSense()
    {
        await using var f = new Fixture();
        var view = await f.Preferences.GetAsync(Parent, default);
        view.Should().OnlyContain(x => !x.Sms && !x.WhatsApp);
        view.Where(x => x.TextAvailable).Select(x => x.Category).Should().BeEquivalentTo(new[] { "Sessions", "ParentSummary", "Subscription" });
        view.Single(x => x.Category == "Subscription").EmailEditable.Should().BeFalse();
        await f.Invoking(x => x.Preferences.SaveAsync(Parent, [new("Exams", true, false) { Sms = true }], default)).Should().ThrowAsync<ArgumentException>();
        await f.Invoking(x => x.Preferences.SaveAsync(Parent, [new("Subscription", true, false)], default)).Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ParentSummaryEmailFollowsTheParentsChoice()
    {
        await using var f = new Fixture();
        var policy = new NotificationChannelPolicy(f.Db, f.Directory, f.Mail, Options.Create(new PublicAppUrlOptions { BaseUrl = "https://onuraltintas.net" }));
        (await policy.PlanAsync([Parent], "ParentWeeklySummary", default))[Parent].Email.Should().BeTrue();
        await f.Preferences.SaveAsync(Parent, [new("ParentSummary", true, false)], default);
        (await policy.PlanAsync([Parent], "ParentWeeklySummary", default))[Parent].Email.Should().BeFalse();
        // The summary consumer writes its own e-mail; the generic channel must not add a second one.
        await f.Preferences.SaveAsync(Parent, [new("ParentSummary", true, true)], default);
        await f.Dispatcher.SendAsync(Guid.NewGuid(), [Parent], "Özet", "Metin", "ParentWeeklySummary", null, default);
        f.Mail.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task CommercialConsentIsStoredPerChannelAndWaitsForTheIysIntegration()
    {
        await using var f = new Fixture();
        var consents = new CommercialConsentService(f.Db);
        (await consents.GetAsync(Parent, default)).Should().OnlyContain(x => !x.Granted && x.IysStatus == "NotRecorded");
        var saved = await consents.SaveAsync(Parent, [new("Sms", true), new("Email", false)], default);
        saved.Single(x => x.Channel == "Sms").Should().BeEquivalentTo(new { Granted = true, IysStatus = "Pending" });
        saved.Single(x => x.Channel == "Email").Should().BeEquivalentTo(new { Granted = false, IysStatus = "Pending" });
        await f.Invoking(x => consents.SaveAsync(Parent, [new("Fax", true)], default)).Should().ThrowAsync<ArgumentException>();
        TextMessageQueue.Mask("+905321234567").Should().Be("+90********67");
    }
}
