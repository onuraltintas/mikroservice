using FluentAssertions;
using Notification.Application.Configuration;
using Notification.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class NotificationContractTests
{
    [Fact]
    public void PublicAppUrlOptions_BuildsVerificationLinkFromConfiguredOrigin()
    {
        var options = new PublicAppUrlOptions { BaseUrl = "https://staging.example.test/" };
        var userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        var link = options.BuildEmailVerificationLink(userId, "token with & unsafe");

        link.Should().Be(
            "https://staging.example.test/auth/confirm-email?token=token%20with%20%26%20unsafe&userId=11111111-1111-1111-1111-111111111111");
    }

    [Theory]
    [InlineData("")]
    [InlineData("localhost:4200")]
    [InlineData("https://user:secret@example.test")]
    [InlineData("https://example.test/path?token=leak")]
    [InlineData("ftp://example.test")]
    public void PublicAppUrlOptions_RejectsUnsafeOrigins(string baseUrl)
    {
        PublicAppUrlOptions.IsValidBaseUrl(baseUrl).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:4200")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://[::1]")]
    public void PublicAppUrlOptions_RejectsLoopbackOriginsInProduction(string baseUrl)
    {
        PublicAppUrlOptions.IsValidForEnvironment(baseUrl, isProduction: true).Should().BeFalse();
        PublicAppUrlOptions.IsValidForEnvironment(baseUrl, isProduction: false).Should().BeTrue();
    }

    [Fact]
    public void SupportRequest_SameIdempotencyKeyWithChangedPayload_IsNotEquivalent()
    {
        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            "Ada",
            "Lovelace",
            "ada@example.test",
            "Access",
            "I need help signing in.",
            "support-request-0001");

        supportRequest.HasSamePayload(
                "Ada",
                "Lovelace",
                "ada@example.test",
                "Access",
                "A different request body.")
            .Should().BeFalse();
    }

    [Fact]
    public void SupportRequest_RepeatedEquivalentPayload_IsEquivalentAfterNormalization()
    {
        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            " Ada ",
            "Lovelace",
            "ada@example.test",
            "Access",
            "I need help signing in.",
            "support-request-0001");

        supportRequest.HasSamePayload(
                "Ada",
                "Lovelace",
                "ADA@EXAMPLE.TEST",
                "Access",
                "I need help signing in.")
            .Should().BeTrue();
    }

    [Fact]
    public void SupportForwardDelivery_RetryThenSend_IsSingleDurableWorkItem()
    {
        var delivery = SupportForwardDelivery.Create(Guid.NewGuid());
        delivery.Status.Should().Be(SupportForwardDeliveryStatus.Pending);

        var firstLease = delivery.Claim(DateTime.UtcNow.AddMinutes(5));
        delivery.AttemptCount.Should().Be(1);
        delivery.MarkRetry(
                DateTime.UtcNow.AddSeconds(30),
                "temporary identity outage",
                permanentlyFailed: false,
                leaseToken: firstLease)
            .Should().BeTrue();
        delivery.Status.Should().Be(SupportForwardDeliveryStatus.Pending);

        var secondLease = delivery.Claim(DateTime.UtcNow.AddMinutes(5));
        delivery.AttemptCount.Should().Be(2);
        delivery.MarkSent(DateTime.UtcNow, secondLease).Should().BeTrue();
        delivery.Status.Should().Be(SupportForwardDeliveryStatus.Sent);
        delivery.LeaseToken.Should().BeNull();
    }

    [Fact]
    public void SupportForwardDelivery_StaleLeaseCannotCompleteWork()
    {
        var delivery = SupportForwardDelivery.Create(Guid.NewGuid());
        var lease = delivery.Claim(DateTime.UtcNow.AddMinutes(5));

        delivery.MarkSent(DateTime.UtcNow, Guid.NewGuid()).Should().BeFalse();
        delivery.Status.Should().Be(SupportForwardDeliveryStatus.Processing);
        delivery.MarkSent(DateTime.UtcNow, lease).Should().BeTrue();
    }
}
