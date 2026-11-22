using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Coaching.Curriculum.IntegrationTests;

/// <summary>22.11.2026: texts written with " · " get the new separators; a goal the student named herself keeps its title.</summary>
public sealed class CommaSeparatorsMigrationPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task Generated_texts_get_the_new_separators_and_nothing_else_changes()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; var topic = await f.Seed();
        var plan = await f.Db.StudyPlanRevisions.SingleAsync();
        var review = StudyPlanTask.Create(plan, new(2026, 10, 20), "Tekrar · Çarpanlar", 10, topic.Id);
        review.SetPlanDecision("Spaced", "Öne almak istediğin konu · Bu dersi zor bulduğun için süre artırıldı");
        var own = StudyPlanTask.Create(plan, new(2026, 10, 21), "Benim · notum", 30);
        var program = TargetUniversityProgram.Create("p-test", "Ankara Üniversitesi", "Hukuk", null, "Bachelor", "EA"); program.SetActive(true);
        var programGoal = AcademicGoal.Create(plan.StudentId, "Ankara Üniversitesi · Hukuk", GoalCategory.ExamPreparation); programGoal.SetCatalogTarget(program.Id, null);
        var ownGoal = AcademicGoal.Create(plan.StudentId, "Benim · hedefim", GoalCategory.ExamPreparation);
        f.Db.AddRange(review, own, program, programGoal, ownGoal); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();

        var sql = Assert.Single(new CommaSeparators().UpOperations.OfType<SqlOperation>()).Sql;
        await f.Db.Database.ExecuteSqlRawAsync(sql);
        await f.Db.Database.ExecuteSqlRawAsync(sql); // idempotent

        var tasks = await f.Db.StudyPlanTasks.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal("Tekrar: Çarpanlar", tasks[review.Id].Title);
        Assert.Equal("Öne almak istediğin konu. Bu dersi zor bulduğun için süre artırıldı", tasks[review.Id].PlanReason);
        Assert.Equal("Benim · notum", tasks[own.Id].Title);
        var goals = await f.Db.AcademicGoals.AsNoTracking().ToDictionaryAsync(x => x.Id);
        Assert.Equal("Ankara Üniversitesi, Hukuk", goals[programGoal.Id].Title);
        Assert.Equal("Benim · hedefim", goals[ownGoal.Id].Title);
        Assert.Empty(new CommaSeparators().DownOperations);
    }
}
