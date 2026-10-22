using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class StudentGoalExperienceTests
{
    [Fact]
    public void UniversityGoalKeepsTytAndAytNetTargetsSeparately()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Üniversite hedefim", GoalCategory.ExamPreparation);
        goal.SetNetTarget("TYT", 85m);
        goal.SetNetTarget("AYT_SAY", 60m);
        Assert.Equal(2, goal.NetTargets.Count);
        Assert.Contains(goal.NetTargets, x => x.ExamCode == "TYT" && x.TargetNet == 85m);
        Assert.Contains(goal.NetTargets, x => x.ExamCode == "AYT_SAY" && x.TargetNet == 60m);
        goal.SetNetTarget("TYT", 90m);
        Assert.Equal(2, goal.NetTargets.Count);
        Assert.Contains(goal.NetTargets, x => x.ExamCode == "TYT" && x.TargetNet == 90m);
    }

    [Fact]
    public void ClearingNetTargetsDoesNotClearCatalogOrProgress()
    {
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Hedef", GoalCategory.ExamPreparation);
        var school = Guid.NewGuid(); goal.SetCatalogTarget(null, school);
        goal.UpdateProgress(40); goal.SetNetTarget("LGS", 75m);
        goal.SetNetTarget(null, null);
        Assert.Empty(goal.NetTargets);
        Assert.Equal(school, goal.TargetSchoolId);
        Assert.Equal(40, goal.CurrentProgress);
    }
}
