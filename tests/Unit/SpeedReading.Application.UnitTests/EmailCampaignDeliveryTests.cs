using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SpeedReading.Application.Content;
using SpeedReading.Application.Notifications;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class EmailCampaignDeliveryTests
{
    [Fact]
    public async Task Immediate_campaign_queue_uses_only_active_consented_confirmed_subscribers()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var dataProtection = new EphemeralDataProtectionProvider();
        var service = CreateCampaigns(db, email, dataProtection, new MutableTimeProvider(DateTimeOffset.UtcNow));
        await AddSubscriberAsync(db, "confirmed@example.com", "Active", true, dataProtection);
        await AddSubscriberAsync(db, "legacy-consent@example.com", "Active", true, dataProtection, hasNewsletterConsent: false);
        await AddSubscriberAsync(db, "pending@example.com", "PendingConfirmation", false, dataProtection);
        await AddSubscriberAsync(db, "legacy@example.com", "LegacyUnconfirmed", false, dataProtection);
        await AddSubscriberAsync(db, "unsubscribed@example.com", "Unsubscribed", false, dataProtection);

        var draft = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            default);
        var result = await service.SendAsync(draft.Id, new SendEmailCampaignRequest(true), default);

        result.Should().NotBeNull();
        result!.Status.Should().Be(2);
        result.TotalRecipients.Should().Be(1);
        result.QueuedCount.Should().Be(1);
        result.SentCount.Should().Be(0);
        result.SentAt.Should().BeNull("queue acceptance is not delivery confirmation");
        email.Messages.Should().ContainSingle();
        email.Messages.Single().Recipient.Should().Be("confirmed@example.com");
        email.Messages.Single().Body.Should().Contain("/newsletter/unsubscribe?token=");

        var stats = await service.GetStatsAsync(draft.Id, default);
        stats.Should().NotBeNull();
        stats!.QueuedCount.Should().Be(1);
        stats.SentCount.Should().Be(0);
    }

    [Fact]
    public async Task Scheduled_campaign_is_queued_only_when_due()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var dataProtection = new EphemeralDataProtectionProvider();
        var clock = new MutableTimeProvider(DateTimeOffset.UtcNow);
        var service = CreateCampaigns(db, email, dataProtection, clock);
        await AddSubscriberAsync(db, "confirmed@example.com", "Active", true, dataProtection);
        var dueAt = clock.GetUtcNow().AddMinutes(2);

        var scheduled = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(scheduledFor: dueAt.UtcDateTime),
            default);
        scheduled.Status.Should().Be(1);
        (await service.ProcessDueAsync(default)).Should().Be(0);
        email.Messages.Should().BeEmpty();

        clock.Set(dueAt);
        (await service.ProcessDueAsync(default)).Should().Be(1);
        email.Messages.Should().ContainSingle();
        (await service.GetAsync(scheduled.Id, default))!.Campaign.Status.Should().Be(2);
    }

    [Theory]
    [InlineData("Student", null, false, true)]
    [InlineData(null, "11111111-1111-1111-1111-111111111111", false, true)]
    [InlineData(null, null, true, true)]
    [InlineData(null, null, false, false)]
    public async Task Campaign_cannot_target_unconsented_general_users(
        string? targetRoles,
        string? targetInstitutionId,
        bool includeAllUsers,
        bool includeSubscribers)
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var dataProtection = new EphemeralDataProtectionProvider();
        var service = CreateCampaigns(db, email, dataProtection, new MutableTimeProvider(DateTimeOffset.UtcNow));
        var request = CreateRequest(
            targetRoles: targetRoles,
            targetInstitutionId: targetInstitutionId is null ? null : Guid.Parse(targetInstitutionId),
            includeAllUsers: includeAllUsers,
            includeSubscribers: includeSubscribers);

        var create = () => service.CreateAsync(Guid.NewGuid(), request, default);

        await create.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Newsletter campaigns can target only consented, confirmed newsletter subscribers.");
        email.Messages.Should().BeEmpty();
    }

    private static OwnedSpeedReadingDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ISpeedReadingEmailCampaigns CreateCampaigns(
        OwnedSpeedReadingDbContext db,
        RecordingEmailDelivery email,
        IDataProtectionProvider dataProtection,
        MutableTimeProvider timeProvider)
    {
        var campaignType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Legacy.LegacySpeedReadingEmailCampaigns", throwOnError: true)!;
        var nullLoggerType = typeof(NullLogger<>).MakeGenericType(campaignType);
        var logger = nullLoggerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static)!.GetValue(null);
        return (ISpeedReadingEmailCampaigns)Activator.CreateInstance(
            campaignType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args: [db, email, dataProtection, timeProvider, "https://masterhizliokuma.com", logger],
            culture: null)!;
    }

    private static CreateEmailCampaignRequest CreateRequest(
        DateTime? scheduledFor = null,
        string? targetRoles = null,
        Guid? targetInstitutionId = null,
        bool includeAllUsers = false,
        bool includeSubscribers = true) => new(
            "Hızlı Okuma bülteni",
            "Yeni içerikler",
            "<p>Bu ayın içerikleri</p>",
            null,
            targetRoles,
            targetInstitutionId,
            includeAllUsers,
            includeSubscribers,
            scheduledFor);

    private static async Task AddSubscriberAsync(
        OwnedSpeedReadingDbContext db,
        string email,
        string status,
        bool isActive,
        IDataProtectionProvider dataProtection,
        bool hasNewsletterConsent = true)
    {
        var entityType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Legacy.LegacyNewsletterSubscriber", throwOnError: true)!;
        var entity = Activator.CreateInstance(entityType)!;
        var now = DateTime.UtcNow;
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        entityType.GetProperty("Id")!.SetValue(entity, Guid.NewGuid());
        entityType.GetProperty("Email")!.SetValue(entity, email);
        entityType.GetProperty("Status")!.SetValue(entity, status);
        entityType.GetProperty("IsActive")!.SetValue(entity, isActive);
        entityType.GetProperty("ConsentedAt")!.SetValue(entity, now);
        entityType.GetProperty("ConfirmedAt")!.SetValue(entity, isActive ? now : null);
        entityType.GetProperty("PrivacyPolicyVersion")!.SetValue(entity, 1);
        entityType.GetProperty("ConsentStatementVersion")!.SetValue(
            entity,
            hasNewsletterConsent ? "speed-reading-newsletter-consent-v1" : null);
        entityType.GetProperty("UnsubscribeTokenProtected")!.SetValue(
            entity,
            dataProtection.CreateProtector("EduPlatform.SpeedReading.NewsletterTokens.v1").Protect(token));
        entityType.GetProperty("CreatedAt")!.SetValue(entity, now);
        entityType.GetProperty("CreatedBy")!.SetValue(entity, Guid.Empty);
        db.Add(entity);
        await db.SaveChangesAsync();
    }

    private sealed class RecordingEmailDelivery : ISpeedReadingEmailDelivery
    {
        public List<QueuedEmail> Messages { get; } = [];

        public Task QueueAsync(
            Guid messageId,
            string consumerType,
            string recipient,
            string subject,
            string body,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(new QueuedEmail(messageId, consumerType, recipient, subject, body));
            return Task.CompletedTask;
        }
    }

    private sealed record QueuedEmail(Guid MessageId, string ConsumerType, string Recipient, string Subject, string Body);

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset current = now;
        public override DateTimeOffset GetUtcNow() => current;
        public void Set(DateTimeOffset value) => current = value;
    }
}
