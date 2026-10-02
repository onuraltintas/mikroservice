using Coaching.Application.StudyPlanning;
using Coaching.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class CoachingGoalScoreCalculatorTests
{
    private static GoalScoreEvidence Evidence(decimal score = 320, decimal scale = 500,
        ExamType type = ExamType.LGS, string source = "StudentReported", int day = 2) =>
        new(Guid.NewGuid(), Guid.NewGuid(), source, type, scale, score, new DateTime(2026, 10, day, 0, 0, 0, DateTimeKind.Utc));

    [Theory]
    [InlineData(null, 500, ExamType.LGS)]
    [InlineData(400, null, ExamType.LGS)]
    [InlineData(400, 500, null)]
    [InlineData(0, 500, ExamType.LGS)]
    [InlineData(600, 500, ExamType.LGS)]
    public void MissingOrInvalidConfiguration_HasNoInventedPercentage(decimal? target, decimal? scale, ExamType? type)
    {
        var report = GoalScoreCalculator.Calculate(target, scale, type, null, [Evidence()]);
        Assert.Equal("ScoreTargetNotConfigured", report.Reason);
        Assert.Empty(report.Comparisons);
    }

    [Fact]
    public void SubjectGoal_DoesNotUseWholeExamScore()
    {
        var report = GoalScoreCalculator.Calculate(400, 500, ExamType.LGS, "Matematik", [Evidence()]);
        Assert.Equal("SubjectTargetNotComparable", report.Reason);
        Assert.Empty(report.Comparisons);
    }

    [Fact]
    public void DifferentTypeOrScale_IsNotNormalizedIntoAnUnrelatedTarget()
    {
        var report = GoalScoreCalculator.Calculate(400, 500, ExamType.LGS, null,
            [Evidence(80, 100), Evidence(400, 500, ExamType.YKS), Evidence(-1), Evidence(600)]);
        Assert.Equal("NoMatchingResults", report.Reason);
        Assert.Empty(report.Comparisons);
    }

    [Fact]
    public void LatestMatchingResultPerSource_IsTransparentAndDoesNotMixSources()
    {
        var latest = Evidence(320, day: 2);
        var recorded = Evidence(360, source: "TeacherRecorded");
        var report = GoalScoreCalculator.Calculate(400, 500, ExamType.LGS, null,
            [Evidence(400, day: 1), latest, recorded, Evidence(source: "Unknown")]);
        Assert.Equal("LatestMatchingResultPerSource", report.Reason);
        Assert.Equal(2, report.Comparisons.Count);
        var self = Assert.Single(report.Comparisons.Where(x => x.Source == "StudentReported"));
        Assert.Equal(latest.ResultId, self.ResultId);
        Assert.Equal(latest.ExamDate, self.ExamDate);
        Assert.Equal(320, self.Score);
        Assert.Equal(80, self.TargetAttainmentPercentage);
        Assert.Equal(80, self.RemainingScore);
        Assert.False(self.TargetReached);
        Assert.Equal(90, Assert.Single(report.Comparisons.Where(x => x.Source == "TeacherRecorded")).TargetAttainmentPercentage);
    }

    [Theory]
    [InlineData(400, 100, 0, true)]
    [InlineData(440, 100, 0, true)]
    [InlineData(0, 0, 400, false)]
    [InlineData(333, 83.3, 67, false)]
    public void PercentageIsBoundedAndRounded_WithoutChangingRecordedGoalProgress(decimal score, decimal percent, decimal gap, bool reached)
    {
        var row = Assert.Single(GoalScoreCalculator.Calculate(400, 500, ExamType.LGS, null, [Evidence(score)]).Comparisons);
        Assert.Equal(percent, row.TargetAttainmentPercentage);
        Assert.Equal(gap, row.RemainingScore);
        Assert.Equal(reached, row.TargetReached);
    }

    [Fact]
    public void EqualDateUsesStableResultIdTieBreak()
    {
        var a = Evidence(300) with { ResultId = Guid.Parse("00000000-0000-0000-0000-000000000001") };
        var b = Evidence(350) with { ResultId = Guid.Parse("00000000-0000-0000-0000-000000000002") };
        var first = GoalScoreCalculator.Calculate(400, 500, ExamType.LGS, null, [a, b]);
        var second = GoalScoreCalculator.Calculate(400, 500, ExamType.LGS, null, [b, a]);
        Assert.Equal(first.Comparisons.Single(), second.Comparisons.Single());
    }
}
