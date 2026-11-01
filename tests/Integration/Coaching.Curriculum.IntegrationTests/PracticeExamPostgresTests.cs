using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.AddExamResult;
using Coaching.Application.Commands.UpdateExam;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries.GetExamResults;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PracticeExamPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AllProfilesRoundTripAndMigrationPreservesLegacyResultsGoalsAndCompletedWork()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create("20261024203951_DecisionResponsibilityAndPostponement"); await f.Data.Seed(legacyPlanSchema: true);
        var student = await f.Db.StudyPlanTasks.Select(x=>x.StudentId).SingleAsync();
        f.Data.User.UserId = student; f.Data.User.Roles = ["Student"];
        var access = new CoachingAccessPolicy(f.Data.User);
        var legacy = Exam.CreateStudentReported(student, "Eski deneme", ExamType.Mock, DateTime.UtcNow, 100);
        var legacyResult = ExamResult.Create(legacy.Id, student, 80); legacyResult.SetAnswerStatistics(8, 1, 1);
        legacyResult.SetSubjectScores(new() { ["Matematik"] = 80 }, 100); legacy.AddResult(legacyResult);
        var goal = AcademicGoal.Create(student, "Mevcut hedef", GoalCategory.ExamPreparation);
        // The legacy exam is written on the older schema, before the institution-wide exam columns (roadmap O4).
        await using (var data = new LegacyExamSchemaDbContext(f.Db.Database.GetConnectionString()!))
        {
            data.AddRange(legacy, goal); await data.SaveChangesAsync();
        }
        f.Db.ChangeTracker.Clear();
        Exam original;
        await using (var data = new LegacyExamSchemaDbContext(f.Db.Database.GetConnectionString()!))
            original = await data.Exams.AsNoTracking().Include(x => x.Results).SingleAsync(x => x.Id == legacy.Id);
        var originalResult = original.Results.Single();
        var legacyBefore = JsonSerializer.Serialize(new { original.Id, original.Version, originalResult.Score, originalResult.SubjectScoresJson });
        var taskBefore = JsonSerializer.Serialize(await f.Db.StudyPlanTasks.FromSqlRaw("""SELECT task.*, NULL::uuid AS "CompletionSourceTaskId", '[]'::jsonb AS "RealizationHistoryJson",NULL::integer AS "RemainingMinutes",NULL::integer AS "RemainingQuestions",NULL::uuid AS "PredecessorTaskId",NULL::integer AS "PredecessorVersion",NULL::jsonb AS "ActualWorkJson",CASE WHEN task."ActualMinutes" IS NOT NULL THEN task."PlannedDate" END AS "WorkFromDate",CASE WHEN task."ActualMinutes" IS NOT NULL THEN task."PlannedDate" END AS "WorkToDate",NULL::timestamptz AS "SkippedAt",NULL::varchar AS "PlanKind",NULL::varchar AS "PlanReason" FROM coaching.study_plan_tasks task""").AsNoTracking().SingleAsync());
        var linksBefore = JsonSerializer.Serialize(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync());
        await f.Db.GetService<IMigrator>().MigrateAsync("20261020075034_UnifiedTargetCatalogConstraints");
        await f.Db.Database.MigrateAsync(); await f.Db.Database.MigrateAsync(); f.Db.ChangeTracker.Clear();
        var savedLegacy = await f.Db.Exams.Include(x => x.Results).SingleAsync(x => x.Id == legacy.Id);
        var savedResult = savedLegacy.Results.Single();
        Assert.Equal(legacyBefore, JsonSerializer.Serialize(new { savedLegacy.Id, savedLegacy.Version, savedResult.Score, savedResult.SubjectScoresJson }));
        Assert.Null(savedResult.GetPracticeExam()); Assert.Equal(goal.Id, (await f.Db.AcademicGoals.SingleAsync()).Id);

        var service = new CoachingStudentExamService(f.Db, access);
        foreach (var profile in PracticeExamRules.Profiles)
        {
            var practice = PracticeExamTests.Blank(profile);
            var first = practice.Sections[0];
            practice = practice with { Sections = [first with { Correct = first.Empty - 4, Wrong = 4, Empty = 0 }, .. practice.Sections.Skip(1)] };
            var total = PracticeExamRules.Summarize(practice);
            var request = new StudentExamInput(profile.Name, PracticeExamRules.ExamTypeFor(profile.Code),
                DateOnly.FromDateTime(DateTime.UtcNow), 350, 500, total.Correct, total.Wrong, total.Empty, [], practice);
            var created = await service.CreateAsync(request); f.Db.ChangeTracker.Clear();
            var read = (await service.GetAsync(created.Id))!;
            Assert.Equal(JsonSerializer.Serialize(total), JsonSerializer.Serialize(read.NetSummary));
            Assert.Equal(practice.ExamCode, read.PracticeExam!.ExamCode);
            var bad = request with { PracticeExam = practice with { Sections = [] } };
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(read.Id, new(bad, read.Version)));
            Assert.Equal(JsonSerializer.Serialize(read), JsonSerializer.Serialize(await service.GetAsync(read.Id)));
            first = practice.Sections[0];
            var changed = practice with { Sections = [first with { Correct = first.Correct + 1, Wrong = first.Wrong - 1 }, .. practice.Sections.Skip(1)] };
            var changedTotal = PracticeExamRules.Summarize(changed);
            var updated = await service.ReplaceAsync(read.Id, new(request with { PracticeExam = changed,
                CorrectAnswers = changedTotal.Correct, WrongAnswers = changedTotal.Wrong }, read.Version));
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.ReplaceAsync(read.Id, new(request, read.Version)));
            f.Db.ChangeTracker.Clear(); Assert.Equal(changedTotal.Net, (await service.GetAsync(updated.Id))!.NetSummary!.Net);
        }
        var history = await new GetStudentExamResultsQueryHandler(new ExamRepository(f.Db), access, new Identity())
            .Handle(new(student, PageSize: 50), default);
        Assert.Equal(PracticeExamRules.Profiles.Count, history.Items.Count(x => x.NetSummary is not null));
        Assert.Null(history.Items.Single(x => x.ExamId == legacy.Id).NetSummary);
        var teacherHistory = await new CoachingAdminRepository(f.Db).GetStudentHistoryAsync(student, CoachingStudentHistoryType.Exams, 1, 50);
        Assert.Equal(PracticeExamRules.Profiles.Count, teacherHistory.Items.Count(x => x.NetSummary is not null));
        Assert.All(teacherHistory.Items.Where(x => x.NetSummary is not null), x => Assert.Equal("StudentReported", x.Source));
        Assert.Equal(taskBefore, JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync()));
        Assert.Equal(linksBefore, JsonSerializer.Serialize(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync()));
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeacherCreateReplayAndCorrectionAreVisibleInStudentHistoryWithIdentityPreserved()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        f.Data.User.Roles = ["Teacher"]; var teacher = f.Data.User.UserId!.Value; var student = Guid.NewGuid();
        var exam = Exam.Create(teacher, "YDT deneme", ExamType.YKS, DateTime.UtcNow, 500); f.Db.Add(exam); await f.Db.SaveChangesAsync();
        var access = new CoachingAccessPolicy(f.Data.User); var repository = new ExamRepository(f.Db); var unit = new UnitOfWork(f.Db);
        var practice = new PracticeExamInput("YDT_EN", [new("ENG", 60, 12, 8)]);
        var create = new AddExamResultCommand(exam.Id, student, 350, 60, 12, 8, null, "Not", "practice-test", PracticeExam: practice);
        var handler = new AddExamResultCommandHandler(repository, unit, access, new Identity(), new IdempotencyRepository(f.Db), new CurriculumManagementTests.NoopPublisher());
        await handler.Handle(create, default); await handler.Handle(create, default); f.Db.ChangeTracker.Clear();
        var detailHandler = new GetTeacherExamDetailQueryHandler(repository, access, new Identity());
        var result = (await detailHandler.Handle(new(exam.Id), default))!.Results.Single();
        Assert.Equal(57, result.NetSummary!.Net);
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => handler.Handle(create with { PracticeExam = practice with { Sections = [new("ENG", 61, 11, 8)] } }, default));
        var edit = new UpdateExamResultCommandHandler(repository, unit, access, new CurriculumManagementTests.NoopPublisher(), new Identity());
        var changed = practice with { Sections = [new("ENG", 64, 8, 8)] };
        await edit.Handle(new(exam.Id, result.Id, 370, 64, 8, 8, null, null, "Düzeltildi", changed), default);
        f.Db.ChangeTracker.Clear();
        var updated = (await detailHandler.Handle(new(exam.Id), default))!.Results.Single();
        Assert.Equal(result.Id, updated.Id); Assert.Equal(student, updated.StudentId); Assert.Equal(62, updated.NetSummary!.Net);
        f.Data.User.UserId = student; f.Data.User.Roles = ["Student"];
        var history = await new GetStudentExamResultsQueryHandler(repository, access, new Identity()).Handle(new(student), default);
        Assert.Equal("TeacherRecorded", history.Items.Single().Source); Assert.Equal(62, history.Items.Single().NetSummary!.Net);
    }

    private sealed class Identity : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? institution, bool admin, CancellationToken ct) => Task.FromResult(institution);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => Task.FromResult(studentIds);
    }
}
