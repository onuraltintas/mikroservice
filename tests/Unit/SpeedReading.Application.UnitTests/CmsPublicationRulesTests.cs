using FluentAssertions;
using SpeedReading.Application.Content;

namespace SpeedReading.Application.UnitTests;

public sealed class CmsPublicationRulesTests
{
    [Theory]
    [InlineData(false, null, false)]
    [InlineData(true, null, true)]
    [InlineData(true, "2030-01-01T00:00:00Z", false)]
    [InlineData(true, "2020-01-01T00:00:00Z", true)]
    public void Publishes_only_when_enabled_and_schedule_is_due(
        bool isPublished,
        string? scheduledPublishAt,
        bool expected)
    {
        DateTime? scheduledAt = scheduledPublishAt is null ? null : DateTime.Parse(scheduledPublishAt).ToUniversalTime();

        CmsPublicationRules.IsPubliclyAvailable(isPublished, scheduledAt, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .Should().Be(expected);
    }
}
