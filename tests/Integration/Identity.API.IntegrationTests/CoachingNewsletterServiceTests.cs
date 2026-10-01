using System.Security.Cryptography;
using System.Text;
using Coaching.Application.Newsletters;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingNewsletterServiceTests
{
    [Fact]
    public async Task RequestSubscription_RequiresValidEmailAndExplicitConsent()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);

        var missingConsent = () => newsletter.RequestSubscriptionAsync(new CoachingNewsletterSignupRequest("student@example.com", false));
        var invalidEmail = () => newsletter.RequestSubscriptionAsync(Signup("not-an-email"));

        await missingConsent.Should().ThrowAsync<ArgumentException>();
        await invalidEmail.Should().ThrowAsync<ArgumentException>();
        (await db.CoachingNewsletterSubscribers.CountAsync()).Should().Be(0);
        emailDelivery.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestSubscription_SilentlyIgnoresFilledHoneypot()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);

        await newsletter.RequestSubscriptionAsync(new CoachingNewsletterSignupRequest(
            "bot@example.com",
            true,
            "https://spam.example"));

        (await db.CoachingNewsletterSubscribers.CountAsync()).Should().Be(0);
        emailDelivery.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestSubscription_StoresOnlyTokenHashesAndActivatesOnlyAfterConfirmation()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);

        await newsletter.RequestSubscriptionAsync(Signup("  Learner@Example.com "));

        var subscriber = await db.CoachingNewsletterSubscribers.SingleAsync();
        subscriber.Email.Should().Be("learner@example.com");
        subscriber.Status.Should().Be(CoachingNewsletterStatuses.PendingConfirmation);
        subscriber.ConsentTextVersion.Should().Be("privacy-v1;newsletter-consent-v1");
        subscriber.ConsentedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        subscriber.ConfirmedAt.Should().BeNull();
        emailDelivery.Messages.Should().ContainSingle();

        var confirmationToken = ExtractToken(emailDelivery.Messages[0].Body, "/coaching/newsletter/confirm?token=");
        subscriber.ConfirmationTokenHash.Should().Be(HashToken(confirmationToken));
        subscriber.ConfirmationTokenHash.Should().NotBe(confirmationToken);
        (await newsletter.GetSubscribersAsync(null, null, 1, 25)).Items.Single().Email.Should().Be(subscriber.Email);

        (await newsletter.ConfirmSubscriptionAsync(confirmationToken)).Should().BeTrue();
        subscriber.Status.Should().Be(CoachingNewsletterStatuses.Active);
        subscriber.ConfirmedAt.Should().NotBeNull();
        subscriber.ConfirmationTokenHash.Should().BeNull();
        (await newsletter.ConfirmSubscriptionAsync(confirmationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task RequestSubscription_DoesNotSendAnotherConfirmationToAnActiveSubscriber()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);
        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));
        var confirmationToken = ExtractToken(emailDelivery.Messages[0].Body, "/coaching/newsletter/confirm?token=");
        (await newsletter.ConfirmSubscriptionAsync(confirmationToken)).Should().BeTrue();

        await newsletter.RequestSubscriptionAsync(Signup("LEARNER@example.com"));

        (await db.CoachingNewsletterSubscribers.CountAsync()).Should().Be(1);
        emailDelivery.Messages.Should().ContainSingle();
    }

    [Fact]
    public async Task RequestSubscription_RejectsStalePrivacyPolicyVersion()
    {
        await using var db = CreateDbContext();
        var delivery = new RecordingEmailDelivery();
        var newsletter = new CoachingNewsletterService(db, delivery, new FixedPrivacyVersionProvider(2), "https://onuraltintas.net");

        var request = () => newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));

        await request.Should().ThrowAsync<CoachingNewsletterPrivacyPolicyChangedException>();
        (await db.CoachingNewsletterSubscribers.CountAsync()).Should().Be(0);
        delivery.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestSubscription_RequiresFreshConfirmationWhenActiveSubscriberHasOlderLegalVersions()
    {
        await using var db = CreateDbContext();
        var delivery = new RecordingEmailDelivery();
        var legalPages = new FixedPrivacyVersionProvider(1);
        var newsletter = new CoachingNewsletterService(db, delivery, legalPages, "https://onuraltintas.net");
        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));
        var initialToken = ExtractToken(delivery.Messages[0].Body, "/coaching/newsletter/confirm?token=");
        (await newsletter.ConfirmSubscriptionAsync(initialToken)).Should().BeTrue();

        legalPages.Version = 2;
        var staleRequest = () => newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));
        await staleRequest.Should().ThrowAsync<CoachingNewsletterPrivacyPolicyChangedException>();

        await newsletter.RequestSubscriptionAsync(new CoachingNewsletterSignupRequest(
            "learner@example.com", true, PrivacyPolicyVersion: 2, NewsletterConsentVersion: 2));

        var subscriber = await db.CoachingNewsletterSubscribers.SingleAsync();
        subscriber.Status.Should().Be(CoachingNewsletterStatuses.PendingConfirmation);
        subscriber.ConsentTextVersion.Should().Be("privacy-v2;newsletter-consent-v2");
        delivery.Messages.Should().HaveCount(2);
    }

    [Fact]
    public async Task RequestSubscription_DoesNotResendConfirmationToPendingSubscriberDuringCooldown()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);
        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));
        var subscriber = await db.CoachingNewsletterSubscribers.SingleAsync();
        var confirmationTokenHash = subscriber.ConfirmationTokenHash;

        await newsletter.RequestSubscriptionAsync(Signup("LEARNER@example.com"));

        emailDelivery.Messages.Should().ContainSingle();
        subscriber.ConfirmationTokenHash.Should().Be(confirmationTokenHash);
    }

    [Fact]
    public async Task RequestSubscription_AllowsPendingConfirmationResendAfterCooldown()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);
        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));
        var subscriber = await db.CoachingNewsletterSubscribers.SingleAsync();
        subscriber.UpdatedAt = DateTime.UtcNow.AddMinutes(-2);

        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));

        emailDelivery.Messages.Should().HaveCount(2);
        subscriber.ConfirmationTokenHash.Should().NotBeNull();
    }

    [Fact]
    public async Task Unsubscribe_RevokesAnActiveSubscriptionAndFreshConsentStartsPendingAgain()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);
        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));
        var firstMessage = emailDelivery.Messages.Single();
        var confirmationToken = ExtractToken(firstMessage.Body, "/coaching/newsletter/confirm?token=");
        var unsubscribeToken = ExtractToken(firstMessage.Body, "/coaching/newsletter/unsubscribe?token=");
        (await newsletter.ConfirmSubscriptionAsync(confirmationToken)).Should().BeTrue();

        (await newsletter.UnsubscribeAsync(unsubscribeToken)).Should().BeTrue();
        var subscriber = await db.CoachingNewsletterSubscribers.SingleAsync();
        subscriber.Status.Should().Be(CoachingNewsletterStatuses.Unsubscribed);
        subscriber.UnsubscribedAt.Should().NotBeNull();

        await newsletter.RequestSubscriptionAsync(Signup("learner@example.com"));

        subscriber.Status.Should().Be(CoachingNewsletterStatuses.PendingConfirmation);
        subscriber.ConfirmedAt.Should().BeNull();
        emailDelivery.Messages.Should().HaveCount(2);
        var renewedToken = ExtractToken(emailDelivery.Messages[1].Body, "/coaching/newsletter/confirm?token=");
        renewedToken.Should().NotBe(confirmationToken);
        (await newsletter.ConfirmSubscriptionAsync(renewedToken)).Should().BeTrue();
        subscriber.Status.Should().Be(CoachingNewsletterStatuses.Active);
    }

    [Fact]
    public async Task AdminCanFilterExportUnsubscribeAndEraseSubscribers()
    {
        await using var db = CreateDbContext();
        var (newsletter, emailDelivery) = CreateService(db);
        await newsletter.RequestSubscriptionAsync(Signup("active@example.com"));
        var activeToken = ExtractToken(emailDelivery.Messages[0].Body, "/coaching/newsletter/confirm?token=");
        (await newsletter.ConfirmSubscriptionAsync(activeToken)).Should().BeTrue();
        await newsletter.RequestSubscriptionAsync(Signup("pending@example.com"));

        var activePage = await newsletter.GetSubscribersAsync("active@", CoachingNewsletterStatuses.Active, 1, 25);
        activePage.TotalCount.Should().Be(1);
        activePage.Items.Single().Email.Should().Be("active@example.com");
        (await newsletter.ExportSubscribersAsync(CoachingNewsletterStatuses.Active)).Should().ContainSingle();

        var actorId = Guid.NewGuid();
        var activeId = activePage.Items.Single().Id;
        (await newsletter.UnsubscribeSubscriberAsync(activeId, actorId)).Should().BeTrue();
        (await db.CoachingNewsletterSubscribers.SingleAsync(item => item.Id == activeId)).Status
            .Should().Be(CoachingNewsletterStatuses.Unsubscribed);
        (await newsletter.DeleteSubscriberAsync(activeId, actorId)).Should().BeTrue();
        (await db.CoachingNewsletterSubscribers.AnyAsync(item => item.Id == activeId)).Should().BeFalse();
    }

    private static (CoachingNewsletterService Service, RecordingEmailDelivery EmailDelivery) CreateService(CoachingDbContext db)
    {
        var delivery = new RecordingEmailDelivery();
        return (new CoachingNewsletterService(db, delivery, new FixedPrivacyVersionProvider(1), "https://onuraltintas.net"), delivery);
    }

    private static CoachingNewsletterSignupRequest Signup(string email) => new(
        email, true, PrivacyPolicyVersion: 1, NewsletterConsentVersion: 1);

    private static CoachingDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static string ExtractToken(string body, string linkPrefix)
    {
        var start = body.IndexOf(linkPrefix, StringComparison.Ordinal);
        start.Should().BeGreaterThanOrEqualTo(0);
        start += linkPrefix.Length;
        var end = body.IndexOfAny(['&', '"', '\'', '<'], start);
        return Uri.UnescapeDataString(body[start..(end < 0 ? body.Length : end)]);
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private sealed class RecordingEmailDelivery : ICoachingNewsletterEmailDelivery
    {
        public List<(Guid MessageId, string Recipient, string Body)> Messages { get; } = [];

        public Task QueueConfirmationAsync(Guid messageId, string recipient, string body, CancellationToken cancellationToken = default)
        {
            Messages.Add((messageId, recipient, body));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedPrivacyVersionProvider(int? version) : ICoachingSharedLegalPageVersionProvider
    {
        public int? Version { get; set; } = version;
        public Task<int?> GetPublishedVersionAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult(Version);
    }
}
