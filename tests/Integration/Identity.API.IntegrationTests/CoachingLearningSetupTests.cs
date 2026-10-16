using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLearningSetupTests
{
    [Fact]
    public async Task ReadDoesNotCreateContext_AndIncompleteSetupKeepsExistingWork()
    {
        await using var f = await Fixture.Create();
        var revision = StudyPlanRevision.Create(f.Student, Guid.NewGuid(), 1, "Koçumun planı"); revision.Activate();
        var task = StudyPlanTask.Create(revision, new(2026, 10, 16), "Korunacak çalışma", 30); task.Complete(25);
        f.Db.AddRange(revision, task); await f.Db.SaveChangesAsync();
        var result = await f.Service.GetAsync(default);
        Assert.Null(result.Context); Assert.Equal("Purpose", result.NextStep);
        Assert.Empty(f.Db.StudentLearningContexts);
        Assert.Equal(25, (await f.Db.StudyPlanTasks.SingleAsync()).ActualMinutes);
        Assert.Equal(StudyPlanStatus.Active, (await f.Db.StudyPlanRevisions.SingleAsync()).Status);
    }

    [Fact]
    public async Task DraftResumesAcrossSessions_WithConditionalExamFieldsAndVersionConflicts()
    {
        await using var f = await Fixture.Create();
        var result = await f.Service.SaveAsync(new(null, 2026, LearningPurpose.Yks, 2027, "DIL", "English", [], [], false), default);
        Assert.Equal("Availability", result.NextStep);
        f.Db.ChangeTracker.Clear();
        var resumed = await f.Service.GetAsync(default);
        Assert.Equal("English", resumed.Context!.ForeignLanguage);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync(new(null, 2026, LearningPurpose.School, null, null, null, [], [], false), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync(new(result.Context!.Version, 2026, LearningPurpose.Yks, 2027, "DIL", null, [], [], false), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync(new(result.Context!.Version, 2026, LearningPurpose.Lgs, null, null, null, [], [], false), default));
        Assert.Single(f.Db.StudentLearningContexts);
    }

    [Fact]
    public async Task PublishedCatalogIsScoped_UnknownDurationsAreVisible_AndStatementsAreNotMastery()
    {
        await using var f = await Fixture.Create();
        var catalog = CurriculumVersion.Create("test-2026", "Test programı", "v1", 2026, 2026, "Test kaynağı", null);
        catalog.SetPublished(true);
        var lesson = StudyCatalogLesson.Create("test", "math", "Matematik", 12, null);
        lesson.AssignCurriculum(catalog.Id); lesson.SetActive(true);
        var wrongGrade = StudyCatalogLesson.Create("test", "wrong", "11. sınıf", 11, null);
        wrongGrade.AssignCurriculum(catalog.Id); wrongGrade.SetActive(true);
        var unit = StudyCatalogUnit.Create("test", "unit", lesson.Id, "Sayılar", 1); unit.SetActive(true);
        var topic = StudyCatalogTopic.Create("test", "topic", lesson.Id, unit.Id, "Doğal sayılar", null, 1); topic.SetActive(true);
        f.Db.AddRange(catalog, lesson, wrongGrade, unit, topic); await f.Db.SaveChangesAsync();
        var saved = await f.Service.SaveAsync(new(null, 2026, LearningPurpose.School, null, null, null, [], [], false), default);
        var choices = await f.Service.GetCatalogAsync(lesson.Id, 1, default);
        Assert.Single(choices.Lessons); Assert.Equal(1, choices.MissingDurationCount);
        Assert.Null(Assert.Single(choices.Topics.Items).EstimatedMinutes);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveAsync(new(saved.Context!.Version, 2026, LearningPurpose.School, null, null, null,
            [new(wrongGrade.Id, 1, null)], [], false), default));
        var hours = StudyAvailability.Create(f.Student, "Europe/Istanbul");
        hours.ReplaceWindows([new(DayOfWeek.Monday, 1020, 1080)]); f.Db.Add(hours); await f.Db.SaveChangesAsync();
        var ready = await f.Service.SaveAsync(new(saved.Context!.Version, 2026, LearningPurpose.School, null, null, null,
            [new(lesson.Id, 1, 3)], [new(topic.Id, DeclaredTopicStatus.Studied)], true), default);
        Assert.True(ready.IsReady); Assert.Equal("Preview", ready.NextStep);
        Assert.Equal("StudentDeclaration", Assert.Single(ready.Context!.TopicStates).Source);
        Assert.Equal(f.Student, Assert.Single(ready.Context.TopicStates).UpdatedBy);
        Assert.Contains(ready.Warnings, x => x.Contains("süre"));
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync();
        profile.UpdateEducation(CoachingEducationStatus.Student, 11, null, null, null, null, null, null);
        await f.Db.SaveChangesAsync();
        Assert.False((await f.Service.GetAsync(default)).IsReady);
    }

    [Fact]
    public async Task ProductAccessRevokedOrTeacher_CannotReadOrWriteStudentSetup()
    {
        await using var f = await Fixture.Create();
        f.Actor.Roles = ["Teacher"];
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.GetAsync(default));
        f.Actor.Roles = ["Student"];
        f.Accounts.Users[f.Student] = f.Accounts.Users[f.Student] with { HasCoachingAccess = false };
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.GetAsync(default));
    }

    [Fact]
    public void CurriculumAndContext_RejectInvalidValues()
    {
        Assert.Throws<ArgumentException>(() => CurriculumVersion.Create("x", "x", "v1", 2027, 2026, "test", null));
        Assert.Throws<ArgumentException>(() => StudentLearningContext.Create(Guid.Empty, 2026, LearningPurpose.School, null, null, null));
        Assert.Throws<ArgumentException>(() => StudentLearningContext.Create(Guid.NewGuid(), 2026, LearningPurpose.Yks, 2027, "UNKNOWN", null));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture Inner { get; } = new();
        public Coaching.Infrastructure.Data.CoachingDbContext Db => Inner.Db;
        public CoachingOwnedProfilePersistenceTests.Actor Actor => Inner.Actor;
        public CoachingOwnedProfilePersistenceTests.FakeAccounts Accounts => Inner.Accounts;
        public Guid Student { get; private set; }
        public CoachingLearningSetupService Service => new(Db, new CoachingAccessPolicy(Actor), Accounts);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Student = f.Inner.AddAccount("Student");
            f.Actor.UserId = f.Student; f.Actor.Roles = ["Student"];
            await f.Inner.Store.ProvisionAsync(f.Student, "Student", default);
            var profile = await f.Db.CoachingStudentProfiles.SingleAsync();
            profile.UpdateEducation(CoachingEducationStatus.Student, 12, null, null, null, null, null, null);
            await f.Db.SaveChangesAsync(); return f;
        }
        public ValueTask DisposeAsync() => Inner.DisposeAsync();
    }
}
