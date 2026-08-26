using System.Text.Json;
using FluentAssertions;
using SpeedReading.Application.Content;
using SpeedReading.Application.Reports;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingReportExportRulesTests
{
    [Fact]
    public void Normalizes_report_metadata_and_nested_data()
    {
        using var document = JsonDocument.Parse("""
            {
              "reportType": "student-detail",
              "title": "Öğrenci Raporu",
              "startDate": "2026-08-01T00:00:00Z",
              "endDate": "2026-08-26T00:00:00Z",
              "data": { "score": 82, "topics": ["hız", "anlama"] }
            }
            """);

        var request = SpeedReadingReportExportRules.Normalize(document.RootElement);

        request.ReportType.Should().Be("student-detail");
        request.Title.Should().Be("Öğrenci Raporu");
        request.StartDate.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        request.EndDate.Should().Be(new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc));
        SpeedReadingReportExportRules.Flatten(request.Data)
            .Should().ContainInOrder(
                new ReportExportRow("score", "82"),
                new ReportExportRow("topics[0]", "hız"),
                new ReportExportRow("topics[1]", "anlama"));
    }

    [Fact]
    public void Uses_a_safe_default_for_empty_payloads_and_rejects_oversized_payloads()
    {
        var request = SpeedReadingReportExportRules.Normalize(null);

        request.ReportType.Should().BeNull();
        SpeedReadingReportExportRules.ResolveTitle(request).Should().Be("Hızlı Okuma Raporu");
        SpeedReadingReportExportRules.Flatten(request.Data).Should().BeEmpty();

        using var oversized = JsonDocument.Parse(
            JsonSerializer.Serialize(new { data = new string('x', SpeedReadingReportSnapshotRules.MaxDataJsonLength + 1) }));
        FluentActions.Invoking(() => SpeedReadingReportExportRules.Normalize(oversized.RootElement))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Unsubscribe_requires_a_guid_token_and_is_not_affected_by_whitespace()
    {
        var subscriberId = Guid.NewGuid();

        SpeedReadingNewsletterRules.TryGetSubscriberId("  " + subscriberId + "  ", out var parsedId)
            .Should().BeTrue();
        parsedId.Should().Be(subscriberId);
        SpeedReadingNewsletterRules.TryGetSubscriberId("not-a-guid", out _).Should().BeFalse();
    }
}
