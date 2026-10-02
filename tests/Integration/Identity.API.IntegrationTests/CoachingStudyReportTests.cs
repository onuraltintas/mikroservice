using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyReportTests
{
    [Fact]
    public void EmptyPeriod_IsUnknownRatherThanZeroAchievement()
    {
        var report = StudyReportCalculator.Calculate(new(2026, 10, 1), new(2026, 10, 2), []);
        Assert.Null(report.CompletionPercentage);
        Assert.Null(report.ActualMinutes);
        Assert.Equal("NoScheduledTasks", report.Reason);
    }

    [Fact]
    public void DraftAndCancelledArchivedWork_DoNotInflateDenominator()
    {
        var student = Guid.NewGuid();
        var active = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Active"); active.Activate();
        var archived = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Old"); archived.Activate(); archived.Archive();
        var draft = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Draft");
        var report = StudyReportCalculator.Calculate(new(2026, 10, 1), new(2026, 10, 2), [
            new(active.Status, new(2026, 10, 1), null, 30, false, null),
            new(archived.Status, new(2026, 10, 2), null, 60, true, 45),
            new(archived.Status, new(2026, 10, 2), null, 90, false, null),
            new(draft.Status, new(2026, 10, 2), null, 90, false, null),
            new(active.Status, new(2026, 9, 30), null, 90, true, 90)]);
        Assert.Equal(2, report.ScheduledTasks);
        Assert.Equal(50m, report.CompletionPercentage);
        Assert.Equal(90, report.PlannedMinutes);
        Assert.Equal(45, report.ActualMinutes);
        Assert.Equal("StudentReported", report.Source);
        Assert.Equal(2, report.Topics.Single().ScheduledTasks);
    }

    [Fact]
    public void PeriodMustBeOrderedAndBounded()
    {
        Assert.Throws<ArgumentException>(() => StudyReportCalculator.Calculate(new(2026, 10, 2), new(2026, 10, 1), []));
        Assert.Throws<ArgumentException>(() => StudyReportCalculator.Calculate(new(2025, 1, 1), new(2026, 10, 1), []));
    }
}
