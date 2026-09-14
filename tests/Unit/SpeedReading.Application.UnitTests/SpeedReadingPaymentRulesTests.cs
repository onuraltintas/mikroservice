using FluentAssertions;
using SpeedReading.Application.Subscription;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingPaymentRulesTests
{
    [Theory]
    [InlineData("SUCCESS", 1, true)]
    [InlineData("success", 1, true)]
    [InlineData("SUCCESS", 0, false)]
    [InlineData("FAILURE", 1, false)]
    [InlineData(null, 1, false)]
    public void Only_a_successful_provider_payment_can_activate_subscription(
        string? paymentStatus,
        int? fraudStatus,
        bool expected)
    {
        SpeedReadingPaymentRules.IsSuccessful(paymentStatus, fraudStatus)
            .Should().Be(expected);
    }

    [Fact]
    public void Resolves_provider_status_without_activating_unknown_results()
    {
        SpeedReadingPaymentRules.ResolveStatus("SUCCESS", 1).Should().Be("Success");
        SpeedReadingPaymentRules.ResolveStatus("FAILURE", 1).Should().Be("Failed");
        SpeedReadingPaymentRules.ResolveStatus(null, null).Should().Be("Pending");
    }

    [Fact]
    public void Calculates_an_expiry_only_for_positive_plan_duration()
    {
        var start = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        SpeedReadingPaymentRules.ResolveEndDate(start, 30)
            .Should().Be(start.AddDays(30));
        SpeedReadingPaymentRules.ResolveEndDate(start, null).Should().BeNull();
        SpeedReadingPaymentRules.ResolveEndDate(start, 0).Should().BeNull();
    }

    [Fact]
    public void Resolves_manual_access_expiry_from_the_plan_when_the_admin_does_not_override_it()
    {
        var start = new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc);
        var overrideEndDate = start.AddDays(14);

        SpeedReadingAccessRules.ResolveEndDate(start, null, 180).Should().Be(start.AddDays(180));
        SpeedReadingAccessRules.ResolveEndDate(start, null, 365).Should().Be(start.AddDays(365));
        SpeedReadingAccessRules.ResolveEndDate(start, overrideEndDate, 180).Should().Be(overrideEndDate);
    }

    [Theory]
    [InlineData("12345678901", true)]
    [InlineData("1234567890", false)]
    [InlineData("1234567890A", false)]
    [InlineData(null, false)]
    public void Requires_a_realistic_turkish_identity_number_shape(string? identityNumber, bool expected)
    {
        SpeedReadingPaymentRules.IsValidIdentityNumber(identityNumber)
            .Should().Be(expected);
    }
}
