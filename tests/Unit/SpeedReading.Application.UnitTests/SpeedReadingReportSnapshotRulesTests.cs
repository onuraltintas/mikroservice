using System.Text.Json;
using FluentAssertions;
using SpeedReading.Application.Reports;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingReportSnapshotRulesTests
{
    [Fact]
    public void Uses_an_empty_object_when_snapshot_data_is_missing()
    {
        SpeedReadingReportSnapshotRules.NormalizeDataJson(null).Should().Be("{}");
    }

    [Fact]
    public void Preserves_json_snapshot_data_and_rejects_oversized_payloads()
    {
        using var document = JsonDocument.Parse("{\"score\":82}");
        SpeedReadingReportSnapshotRules.NormalizeDataJson(document.RootElement)
            .Should().Be("{\"score\":82}");

        using var oversized = JsonDocument.Parse(
            JsonSerializer.Serialize(new string('x', SpeedReadingReportSnapshotRules.MaxDataJsonLength + 1)));
        FluentActions.Invoking(() => SpeedReadingReportSnapshotRules.NormalizeDataJson(oversized.RootElement))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Applies_a_bounded_default_date_range_and_rejects_invalid_ranges()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        SpeedReadingReportSnapshotRules.ResolveDateRange(null, null, now)
            .Should().Be((now.AddDays(-30), now));

        FluentActions.Invoking(() => SpeedReadingReportSnapshotRules.ResolveDateRange(
                now,
                now.AddDays(-1),
                now))
            .Should().Throw<ArgumentException>();

        FluentActions.Invoking(() => SpeedReadingReportSnapshotRules.ResolveDateRange(
                now.AddDays(-367),
                now,
                now))
            .Should().Throw<ArgumentOutOfRangeException>();
    }
}
