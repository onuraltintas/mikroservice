using Coaching.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class CoachingStudyPlanTests
{
    [Fact]
    public void Plan_StartsAsDraftAndArchivesWithoutLosingTasks()
    {
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Weekly plan");
        Assert.False(plan.IsActive);
        plan.Activate();
        Assert.True(plan.IsActive);
        plan.Archive();
        Assert.False(plan.IsActive);
        Assert.Throws<InvalidOperationException>(() => plan.Activate());
    }

    [Fact]
    public void Task_PreservesPlannedAndActualWorkSeparately()
    {
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan");
        var task = StudyPlanTask.Create(plan, new DateOnly(2026, 10, 2), "Review", 30, null, true);
        Assert.Null(task.ActualMinutes);
        Assert.False(task.IsCompleted);
        task.Complete(25);
        task.Complete(25);
        Assert.Equal(30, task.PlannedMinutes);
        Assert.Equal(25, task.ActualMinutes);
        Assert.True(task.IsPinned);
        Assert.Equal(plan.StudentId, task.StudentId);
        Assert.Throws<InvalidOperationException>(() => task.Complete(40));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void Task_RejectsInvalidMinutes(int minutes)
    {
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan");
        Assert.Throws<ArgumentOutOfRangeException>(() => StudyPlanTask.Create(plan, new DateOnly(2026, 10, 2), "Review", minutes));
    }

    [Fact]
    public void Plan_RejectsMissingOwnerAndTaskInArchivedRevision()
    {
        Assert.Throws<ArgumentException>(() => StudyPlanRevision.Create(Guid.Empty, Guid.NewGuid(), 1, "Plan"));
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan");
        plan.Archive();
        Assert.Throws<InvalidOperationException>(() => StudyPlanTask.Create(plan, new DateOnly(2026, 10, 2), "Review", 30));
    }
}
