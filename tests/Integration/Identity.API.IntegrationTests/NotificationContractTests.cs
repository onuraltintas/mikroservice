using FluentAssertions;
using Notification.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class NotificationContractTests
{
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
