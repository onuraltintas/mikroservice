using System.Reflection;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Infrastructure;
using SpeedReading.Infrastructure.Legacy;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingNewsletterServiceTests
{
    [Fact]
    public async Task Signup_requires_explicit_consent_and_the_current_published_privacy_version()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var legalPages = new FixedLegalPageVersionProvider(8);
        var service = CreateService(db, email, legalPages);

        var missingConsent = () => service.RequestSubscriptionAsync(
            new CmsNewsletterSubscriptionRequest("reader@example.com", false, 8, NewsletterConsentVersion: 8));
        var stalePolicy = () => service.RequestSubscriptionAsync(
            new CmsNewsletterSubscriptionRequest("reader@example.com", true, 7, NewsletterConsentVersion: 8));

        await missingConsent.Should().ThrowAsync<ArgumentException>();
        await stalePolicy.Should().ThrowAsync<SharedLegalPageVersionMismatchException>();
        (await service.GetSubscribersAsync(null, null, 1, 25)).TotalCount.Should().Be(0);
        email.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Honeypot_submission_is_silently_ignored()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var service = CreateService(db, email, new FixedLegalPageVersionProvider(8));

        await service.RequestSubscriptionAsync(
            new CmsNewsletterSubscriptionRequest("bot@example.com", true, 8, Honeypot: "filled", NewsletterConsentVersion: 8));

        (await service.GetSubscribersAsync(null, null, 1, 25)).TotalCount.Should().Be(0);
        email.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task Signup_stays_pending_until_a_hashed_single_use_confirmation_token_is_posted()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var service = CreateService(db, email, new FixedLegalPageVersionProvider(8));

        await service.RequestSubscriptionAsync(
            Signup(" Reader@Example.com "));

        var subscriber = (await service.GetSubscribersAsync(null, "PendingConfirmation", 1, 25)).Items.Should().ContainSingle().Subject;
        subscriber.Email.Should().Be("reader@example.com");
        subscriber.PrivacyPolicyVersion.Should().Be(8);
        var consentStatementVersion = await ReadSubscriberPropertyAsync(db, "ConsentStatementVersion");
        consentStatementVersion.Should().Be("speed-reading-newsletter-consent-v8");
        subscriber.ConsentedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        email.Messages.Should().ContainSingle();
        var token = ReadToken(email.Messages.Single().Body, "/newsletter/confirm");
        var storedConfirmationHash = await ReadSubscriberPropertyAsync(db, "ConfirmationTokenHash");
        storedConfirmationHash.Should().NotBe(token);
        storedConfirmationHash.Should().HaveLength(64);

        (await service.ConfirmSubscriptionAsync(token)).Should().BeTrue();
        (await service.ConfirmSubscriptionAsync(token)).Should().BeFalse();
        (await service.GetSubscribersAsync(null, "Active", 1, 25)).Items.Should().ContainSingle();
    }

    [Fact]
    public async Task Resubscription_reuses_a_previously_soft_deleted_address()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var service = CreateService(db, email, new FixedLegalPageVersionProvider(8));
        var existingId = AddSoftDeletedSubscriber(db, "reader@example.com");
        await db.SaveChangesAsync();

        await service.RequestSubscriptionAsync(
            Signup("reader@example.com"));

        var subscribers = db.ChangeTracker.Entries()
            .Where(entry => entry.Metadata.ClrType.Name == "LegacyNewsletterSubscriber")
            .Select(entry => entry.Entity)
            .ToList();
        subscribers.Should().ContainSingle();
        var page = await service.GetSubscribersAsync(null, "PendingConfirmation", 1, 25);
        page.Items.Should().ContainSingle(item => item.Id == existingId);
        email.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task Unsubscribe_uses_a_random_token_and_only_active_consented_subscribers_are_campaign_recipients()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var service = CreateService(db, email, new FixedLegalPageVersionProvider(8));
        await service.RequestSubscriptionAsync(
            Signup("reader@example.com"));
        var confirmation = ReadToken(email.Messages.Single().Body, "/newsletter/confirm");
        (await service.ConfirmSubscriptionAsync(confirmation)).Should().BeTrue();

        var unsubscribeToken = ReadToken(email.Messages.Single().Body, "/newsletter/unsubscribe");
        (await service.UnsubscribeAsync(Guid.NewGuid().ToString())).Should().BeFalse();
        var storedUnsubscribeHash = await ReadSubscriberPropertyAsync(db, "UnsubscribeTokenHash");
        storedUnsubscribeHash.Should().NotBe(unsubscribeToken);
        storedUnsubscribeHash.Should().HaveLength(64);

        (await service.UnsubscribeAsync(unsubscribeToken)).Should().BeTrue();
        (await service.UnsubscribeAsync(unsubscribeToken)).Should().BeTrue();
        (await service.GetSubscribersAsync(null, "Active", 1, 25)).TotalCount.Should().Be(0);
        (await service.GetSubscribersAsync(null, "Unsubscribed", 1, 25)).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Admin_subscriber_list_filters_by_status_and_email_and_pages_results()
    {
        await using var db = CreateDb();
        var email = new RecordingEmailDelivery();
        var service = CreateService(db, email, new FixedLegalPageVersionProvider(8));

        await service.RequestSubscriptionAsync(Signup("one@example.com"));
        await service.RequestSubscriptionAsync(Signup("two@example.com"));
        await service.RequestSubscriptionAsync(Signup("other@elsewhere.net"));
        var tokens = email.Messages.Select(message => ReadToken(message.Body, "/newsletter/confirm")).ToArray();
        await service.ConfirmSubscriptionAsync(tokens[0]);
        await service.ConfirmSubscriptionAsync(tokens[1]);

        var firstPage = await service.GetSubscribersAsync("@example.com", "Active", 1, 1);
        var secondPage = await service.GetSubscribersAsync("@example.com", "Active", 2, 1);

        firstPage.TotalCount.Should().Be(2);
        firstPage.Items.Should().ContainSingle();
        secondPage.Items.Should().ContainSingle();
        firstPage.Items.Single().Email.Should().NotBe(secondPage.Items.Single().Email);
    }

    private static OwnedSpeedReadingDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CmsNewsletterSubscriptionRequest Signup(string email) =>
        new(email, true, 8, NewsletterConsentVersion: 8);

    private static SpeedReadingNewsletterService CreateService(
        OwnedSpeedReadingDbContext db,
        RecordingEmailDelivery email,
        FixedLegalPageVersionProvider legalPages) =>
        new(
            db,
            email,
            legalPages,
            new EphemeralDataProtectionProvider().CreateProtector("newsletter-tests"),
            TimeProvider.System,
            "https://masterhizliokuma.com");

    private static string ReadToken(string body, string path)
    {
        var match = Regex.Match(body, $"{Regex.Escape(path)}\\?token=([A-Za-z0-9_-]{{43}})");
        match.Success.Should().BeTrue("the queued email should contain a one-time link for {0}", path);
        return match.Groups[1].Value;
    }

    private static async Task<string> ReadSubscriberPropertyAsync(OwnedSpeedReadingDbContext db, string propertyName)
    {
        var entityType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Legacy.LegacyNewsletterSubscriber", throwOnError: true)!;
        var setMethod = typeof(DbContext).GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0);
        var set = setMethod.MakeGenericMethod(entityType).Invoke(db, null)!;
        var entity = ((System.Collections.IEnumerable)set).Cast<object>().Should().ContainSingle().Subject;
        return (string)entityType.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetValue(entity)!;
    }

    private static Guid AddSoftDeletedSubscriber(OwnedSpeedReadingDbContext db, string email)
    {
        var entityType = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Legacy.LegacyNewsletterSubscriber", throwOnError: true)!;
        var subscriberId = Guid.NewGuid();
        var subscriber = Activator.CreateInstance(entityType)!;
        entityType.GetProperty("Id")!.SetValue(subscriber, subscriberId);
        entityType.GetProperty("Email")!.SetValue(subscriber, email);
        entityType.GetProperty("IsActive")!.SetValue(subscriber, false);
        entityType.GetProperty("IsDeleted")!.SetValue(subscriber, true);
        entityType.GetProperty("CreatedAt")!.SetValue(subscriber, DateTime.UtcNow);
        entityType.GetProperty("CreatedBy")!.SetValue(subscriber, Guid.Empty);

        var setMethod = typeof(DbContext).GetMethods()
            .Single(method => method.Name == nameof(DbContext.Set)
                && method.IsGenericMethodDefinition
                && method.GetParameters().Length == 0);
        var set = setMethod.MakeGenericMethod(entityType).Invoke(db, null)!;
        set.GetType().GetMethod("Add")!.Invoke(set, [subscriber]);
        return subscriberId;
    }

    private sealed class FixedLegalPageVersionProvider(int version) : ISharedLegalPageVersionProvider
    {
        public Task<int?> GetPublishedVersionAsync(string slug, CancellationToken cancellationToken = default) =>
            Task.FromResult<int?>(version);
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
}
