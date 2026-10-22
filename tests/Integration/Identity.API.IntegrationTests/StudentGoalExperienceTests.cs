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
    [Fact]
    public async Task PrimarySelectionIsOwnedVersionedAndDoesNotDeleteGoals()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); f.Actor.UserId = student; f.Actor.Roles = ["Student"];
        var first = AcademicGoal.Create(student, "İlk", GoalCategory.ExamPreparation);
        var second = AcademicGoal.Create(student, "İkinci", GoalCategory.ExamPreparation);
        var other = AcademicGoal.Create(Guid.NewGuid(), "Başkasının", GoalCategory.ExamPreparation);
        f.Db.AddRange(first, second, other); await f.Db.SaveChangesAsync();
        var controller = new Coaching.API.Controllers.StudentPrimaryGoalController(f.Db, new Coaching.Application.Authorization.CoachingAccessPolicy(f.Actor));
        Assert.IsType<Microsoft.AspNetCore.Mvc.NotFoundResult>(await controller.Save(new(other.Id, null), default));
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.Save(new(first.Id, null), default));
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await controller.Save(new(second.Id, null), default));
        var current = Assert.Single(f.Db.Set<StudentPrimaryGoal>());
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.Save(new(second.Id, current.Version), default));
        Assert.Equal(second.Id, current.GoalId);
        Assert.Equal(3, f.Db.AcademicGoals.Count());
    }

}
