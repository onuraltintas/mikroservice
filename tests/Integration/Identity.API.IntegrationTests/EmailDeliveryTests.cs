using FluentAssertions;
using Notification.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class EmailDeliveryTests
{
    [Fact]
    public void MarkSent_ShouldRejectAStaleWorkerLease()
    {
        var delivery = EmailDelivery.Create(
            Guid.NewGuid(),
            "UserCreatedConsumer",
            "person@example.com",
            "Welcome",
            "protected-body");

        var activeLease = delivery.Claim(DateTime.UtcNow.AddMinutes(5));
        delivery.Claim(DateTime.UtcNow.AddMinutes(5));

        delivery.MarkSent(DateTime.UtcNow, activeLease).Should().BeFalse();
        delivery.Status.Should().Be(EmailDeliveryStatus.Processing);
    }

    [Fact]
    public void MarkSent_ShouldAcceptTheCurrentLease()
    {
        var delivery = EmailDelivery.Create(
            Guid.NewGuid(),
            "UserCreatedConsumer",
            "person@example.com",
            "Welcome",
            "protected-body");

        var activeLease = delivery.Claim(DateTime.UtcNow.AddMinutes(5));

        delivery.MarkSent(DateTime.UtcNow, activeLease).Should().BeTrue();
        delivery.Status.Should().Be(EmailDeliveryStatus.Sent);
        delivery.LeaseToken.Should().BeNull();
    }
}
