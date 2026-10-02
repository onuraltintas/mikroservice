using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class CoachingGoalScoreTargetTests
{
    [Fact]
    public void ScorePrecisionCannotBeSilentlyRoundedByStorage()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Target", GoalCategory.ExamPreparation);
        Assert.Throws<ArgumentException>(() => goal.SetScoreTarget(80.001m, 100, ExamType.Mock));
        Assert.Throws<ArgumentException>(() => goal.SetScoreTarget(80, 100.001m, ExamType.Mock));
        Assert.Null(goal.TargetScore);
    }
    [Fact]
    public void ScoreTarget_IsExplicitAndIndependentOfManualProgress()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Target", GoalCategory.ExamPreparation);
        goal.UpdateProgress(30);
        goal.SetScoreTarget(400, 500, ExamType.LGS);
        Assert.Equal(500, goal.TargetMaxScore);
        Assert.Equal(400, goal.TargetScore);
        Assert.Equal(ExamType.LGS, goal.TargetExamType);
        Assert.Equal(30, goal.CurrentProgress);
        Assert.False(goal.IsCompleted);
        goal.SetScoreTarget(null, null, null);
        Assert.Null(goal.TargetMaxScore);
        Assert.Null(goal.TargetScore);
        Assert.Null(goal.TargetExamType);
    }

    [Fact]
    public void LegacyGoalUpdateCannotKeepStaleScaleForAChangedTarget()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Target", GoalCategory.ExamPreparation);
        goal.SetScoreTarget(400, 500, ExamType.LGS);
        goal.UpdateEditableDetails("Target", null, GoalCategory.ExamPreparation, null, 80, ExamType.Mock, null);
        Assert.Null(goal.TargetMaxScore);
        goal.SetScoreTarget(80, 100, ExamType.Mock);
        goal.SetTarget(targetScore: 90);
        Assert.Null(goal.TargetMaxScore);
    }

    [Theory]
    [InlineData(400, 100)]
    [InlineData(0, 100)]
    [InlineData(80, 0)]
    [InlineData(80, 1000)]
    public void InvalidScaleOrTargetIsRejected(decimal target, decimal scale)
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Target", GoalCategory.ExamPreparation);
        Assert.Throws<ArgumentException>(() => goal.SetScoreTarget(target, scale, ExamType.LGS));
        Assert.Null(goal.TargetMaxScore);
        Assert.Throws<ArgumentException>(() => goal.SetScoreTarget(target, null, ExamType.LGS));
        Assert.Throws<ArgumentException>(() => goal.SetScoreTarget(null, scale, ExamType.LGS));
        Assert.Throws<ArgumentException>(() => goal.SetScoreTarget(target, scale, null));
    }
}
