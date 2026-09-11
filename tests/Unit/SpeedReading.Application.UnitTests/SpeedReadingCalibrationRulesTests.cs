using FluentAssertions;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingCalibrationRulesTests
{
    [Fact]
    public void Calibration_summary_calculates_distribution_without_exposing_students()
    {
        var rows = Enumerable.Range(1, 40)
            .Select(index => new SpeedReadingCalibrationObservation(
                AssessmentAttemptPhase.Baseline,
                "tr-standard-v1",
                "12-14",
                100 + index,
                50 + index / 2m))
            .ToArray();

        var summary = SpeedReadingCalibrationRules.Summarize(rows).Single();

        summary.SampleSize.Should().Be(40);
        summary.IsPublishable.Should().BeTrue();
        summary.MeanWpm.Should().Be(120.5m);
        summary.MedianWpm.Should().Be(120.5m);
        summary.StandardDeviationWpm.Should().BeGreaterThan(0);
        summary.StudentIdentifiersExposed.Should().BeFalse();
    }

    [Fact]
    public void Small_segments_are_flagged_as_pilot_evidence_only()
    {
        var rows = Enumerable.Range(1, 12)
            .Select(index => new SpeedReadingCalibrationObservation(
                AssessmentAttemptPhase.Retention,
                "tr-standard-v1",
                "adult",
                200 + index,
                70))
            .ToArray();

        var summary = SpeedReadingCalibrationRules.Summarize(rows).Single();

        summary.IsPublishable.Should().BeFalse();
        summary.EvidenceStatus.Should().Be("PilotOnly");
    }
}
