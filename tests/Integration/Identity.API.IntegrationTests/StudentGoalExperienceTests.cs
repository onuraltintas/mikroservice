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
        f.Db.Add(StudentLearningContext.Create(student, 2026, LearningPurpose.Lgs, 2027, null, null));
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

    [Fact]
    public async Task YksLanguageStudentCanChooseALanguageProgramAndYdtNetTargetsAsTheMainGoal()
    {
        // The profile stores the YKS language field as DIL; programs and net targets name the same exam YDT.
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var student = f.AddAccount("Student"); f.Actor.UserId = student; f.Actor.Roles = ["Student"];
        f.Db.Add(StudentLearningContext.Create(student, 2026, LearningPurpose.Yks, 2027, "DIL", "English"));
        var language = Coaching.Domain.Entities.TargetUniversityProgram.Create("ydt-1", "Üniversite", "İngilizce Öğretmenliği", null, "Bachelor", "DIL");
        var numeric = Coaching.Domain.Entities.TargetUniversityProgram.Create("say-1", "Üniversite", "Bilgisayar Mühendisliği", null, "Bachelor", "SAY");
        language.SetActive(true); numeric.SetActive(true);
        var goal = AcademicGoal.Create(student, "Dil hedefim", GoalCategory.ExamPreparation);
        goal.SetCatalogTarget(language.Id, null); goal.SetNetTarget("YDT_EN", 70m);
        f.Db.AddRange(language, numeric, goal); await f.Db.SaveChangesAsync();

        var controller = new Coaching.API.Controllers.StudentPrimaryGoalController(f.Db, new Coaching.Application.Authorization.CoachingAccessPolicy(f.Actor));
        Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(await controller.Save(new(goal.Id, null), default));

        var targets = new Coaching.Infrastructure.StudyPlanning.CoachingGoalTargetService(f.Db,
            new Coaching.Application.Authorization.CoachingAccessPolicy(f.Actor), new NoEvents());
        var view = await targets.ReplaceNetAsync(goal.Id, new(goal.Version, "YDT_EN", 75m));
        Assert.Contains(view.NetTargets!, x => x.ExamCode == "YDT_EN" && x.TargetNet == 75m);
        await Assert.ThrowsAsync<ArgumentException>(() => targets.ReplaceNetAsync(goal.Id, new(view.Version, "AYT_SAY", 40m)));
        await Assert.ThrowsAsync<ArgumentException>(() => targets.ReplaceAsync(goal.Id, new(view.Version, numeric.Id, null)));
    }

    private sealed class NoEvents : Coaching.Application.Interfaces.ICoachingEventPublisher
    {
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class => Task.CompletedTask;
    }

}
