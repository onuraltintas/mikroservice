using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class CurriculumPostgresTests
{
    [LocalPostgresFact]
    public async Task ExamDateMigrationAndRoundTripPreserveExistingCatalogAndWork()
    {
        await using var f=await Pg.Create("20261020065330_MebiLgsProgramCatalog");await f.Data.Seed(legacyPlanSchema: true);
        var taskBefore=JsonSerializer.Serialize(await CurriculumManagementTests.LegacyStudyTasks(f.Db).AsNoTracking().SingleAsync());
        await f.Db.GetService<IMigrator>().MigrateAsync("20261016201006_GradeIndependentCurriculum");
        await f.Db.Database.MigrateAsync();await f.Db.Database.MigrateAsync();f.Db.ChangeTracker.Clear();
        Assert.All(await f.Db.Set<StudyCatalogExam>().ToArrayAsync(),x=>Assert.Null(x.ExamDate));
        var exam=await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="LGS");
        var doc=await f.Service.GetAsync("exams",exam.Id,default);
        await f.Service.SaveAsync("exams",doc.Id,new(doc.Name,"Sınav tarihini belirle",Fingerprint:doc.Fingerprint,Code:"LGS",ExamDate:new(2027,6,20)),default);
        f.Db.ChangeTracker.Clear();
        Assert.Equal(new DateOnly(2027,6,20),(await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Id==exam.Id)).ExamDate);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.SaveAsync("exams",doc.Id,new(doc.Name,"Eski tarih formunu kaydet",Fingerprint:doc.Fingerprint,Code:"LGS",ExamDate:new(2027,6,27)),default));
        var listed=Assert.Single((await f.Service.ChildrenAsync("exams",null,"Liselere",1,default)).Items);
        Assert.Equal(new DateOnly(2027,6,20),listed.ExamDate);
        await f.Service.SaveAsync("exams",listed.Id,new(listed.Name,"Sınav tarihini temizle",Fingerprint:(await f.Service.GetAsync("exams",listed.Id,default)).Fingerprint,Code:"LGS"),default);
        Assert.Null((await f.Db.Set<StudyCatalogExam>().AsNoTracking().SingleAsync(x=>x.Id==exam.Id)).ExamDate);
        Assert.Equal(taskBefore,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync()));
    }

    [LocalPostgresFact]
    public async Task MigrationBackfillsOriginalText_AndRenamesMovesAndDurationUpdatesPreserveCompletedPlan()
    {
        await using var f = await Pg.Create("20261020065330_MebiLgsProgramCatalog"); var topic = await f.Data.Seed(legacyPlanSchema: true);
        var taskBefore = JsonSerializer.Serialize(await CurriculumManagementTests.LegacyStudyTasks(f.Db).AsNoTracking().SingleAsync());
        await f.Db.GetService<IMigrator>().MigrateAsync("20261016180740_StudyCatalogOutcomes");
        await f.Db.Database.MigrateAsync(); await f.Db.Database.MigrateAsync(); f.Db.ChangeTracker.Clear();
        var snapshot = await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync();
        Assert.Equal("İlk kazanım metni",snapshot.NameSnapshot); Assert.Equal("M.8.1.1.1",snapshot.CodeSnapshot);
        var doc = await f.Service.GetAsync("outcomes",snapshot.OutcomeId,default);
        await f.Service.SaveAsync("outcomes",doc.Id,new("Yeni metin","Metni düzelt",Fingerprint:doc.Fingerprint,Code:"M.8.1.1.1"),default);
        var task = await f.Db.StudyPlanTasks.SingleAsync(); f.Data.User.UserId = task.StudentId; f.Data.User.Roles = ["Student"];
        var access = new CoachingAccessPolicy(f.Data.User);
        var plans = new CoachingManualStudyPlanService(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
        var plan = (await plans.GetAsync(task.RevisionId))!;
        Assert.Equal("İlk kazanım metni",Assert.Single(plan.Tasks.Single().Outcomes!).Name);
        Assert.Equal(30,plan.Tasks.Single().Outcomes![0].AllocatedMinutes);
        Assert.Equal(taskBefore,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync()));
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    [LocalPostgresFact]
    public async Task RelationalHierarchySearchAndMoveCopyChildrenAndKeepForeignKeysAndUnrelatedMappings()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var lesson = await f.Db.StudyCatalogLessons.SingleAsync();
        var group = await f.Service.GetAsync("exams",(await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="LGS")).Id,default);
        var lessonDoc = await f.Service.GetAsync("lessons",lesson.Id,default);
        await Assert.ThrowsAsync<ArgumentException>(async ()=>await f.Service.MoveAsync("lessons",lesson.Id,new((await f.Db.Set<StudyCatalogExam>().SingleAsync(x=>x.Code=="TYT")).Id,lessonDoc.Fingerprint,"Kapsamı farklı sınava taşı"),default));
        Assert.Equal(lesson.Id,Assert.Single((await f.Service.ChildrenAsync("lessons",group.Id,"Mat",1,default)).Items).Id);
        var target = await f.Service.SaveAsync("units",null,new("Yeni ünite","Ünite ekle",ParentId:lesson.Id),default);
        target = await f.Service.SetActiveAsync("units",target.Id,new(target.Fingerprint,"Hedef üniteyi etkinleştir",true),default);
        var doc = await f.Service.GetAsync("topics",topic.Id,default);
        Assert.Contains("Matematik",doc.Path); Assert.Contains("LGS",doc.Path); Assert.False(doc.CanDelete);
        var taskBefore = JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync());
        var moved = await f.Service.MoveAsync("topics",topic.Id,new(target.Id,doc.Fingerprint,"Konuyu yeni üniteye taşı"),default);
        Assert.NotEqual(topic.Id,moved.Id); Assert.Single((await f.Service.ChildrenAsync("topics",target.Id,null,1,default)).Items);
        var paths = await f.Service.ChildrenAsync("topics",null,"Çarpanlar",1,default,true);
        Assert.Equal(2,paths.TotalCount); Assert.All(paths.Items,x => Assert.Contains("Matematik",x.Path));
        Assert.Equal(taskBefore,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync()));
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var unused = await f.Service.SaveAsync("outcomes",null,new("Yeni kazanım","Kazanım ekle",ParentId:moved.Id,Code:"M.8.1.1.2",OfficialTopicId:outcome.OfficialTopicId,SourcePage:73,DisplayOrder:2),default);
        await f.Service.DeleteAsync("outcomes",unused.Id,new(unused.Fingerprint,"Kullanılmayan kazanımı sil",unused.Id),default);
        Assert.Single(await f.Db.Set<StudyCatalogOutcome>().ToArrayAsync());
    }

    [LocalPostgresFact]
    public async Task UnitMoveCopiesEntireBranchAndSharedOutcomes_AndArchivesOldTreeWithoutChangingPlans()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var child = StudyCatalogTopic.Create("test","child",topic.LessonId,topic.UnitId,"Alt konu",topic.Id,1,30); child.SetActive(true); f.Db.Add(child);
        var other = StudyCatalogLesson.Create("test","new-lesson","Matematik",8,"LGS"); other.SetActive(true); f.Db.Add(other); await f.Db.SaveChangesAsync();
        var doc = await f.Service.GetAsync("units",topic.UnitId,default);
        var moved = await f.Service.MoveAsync("units",topic.UnitId,new(other.Id,doc.Fingerprint,"Üniteyi aynı kapsamlı derse taşı"),default);
        var copies = await f.Db.StudyCatalogTopics.Where(x => x.UnitId == moved.Id).ToArrayAsync();
        Assert.Equal(2,copies.Length); Assert.Equal(copies.Single(x => x.ParentId == null).Id,copies.Single(x => x.ParentId != null).ParentId);
        Assert.All(await f.Db.StudyCatalogTopics.Where(x => x.UnitId == topic.UnitId).ToArrayAsync(),x => Assert.False(x.IsActive));
        Assert.Equal(topic.Id,(await f.Db.StudyPlanTasks.SingleAsync()).TopicId);
        Assert.Equal(2,await f.Db.Set<StudyCatalogTopicOutcome>().CountAsync());
    }

    [LocalPostgresFact]
    public async Task ConcurrentEditRejectsOldFingerprint_EvenWhenContextPreviouslyReadRecord()
    {
        await using var f = await Pg.Create(); var topic = await f.Data.Seed();
        var doc = await f.Service.GetAsync("topics",topic.Id,default);
        await using (var second = new CoachingDbContext(f.Options))
        { var t = await second.StudyCatalogTopics.SingleAsync(); t.Edit("Diğer yöneticinin değişikliği",1,90); await second.SaveChangesAsync(); }
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("topics",topic.Id,new("Eski form","Eski formu kaydet",Fingerprint:doc.Fingerprint,EstimatedMinutes:45),default));
        Assert.Equal("Diğer yöneticinin değişikliği",(await f.Db.StudyCatalogTopics.AsNoTracking().SingleAsync()).Name);
    }

    [LocalPostgresFact]
    public async Task LongValidOutcomeTextAndTurkishReasonFitAuditWithoutLosingCatalogText()
    {
        await using var f=await Pg.Create();await f.Data.Seed();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var doc=await f.Service.GetAsync("outcomes",outcome.Id,default);var text=new string('ç',2000);var reason=new string('ş',500);
        await f.Service.SaveAsync("outcomes",outcome.Id,new(text,reason,Fingerprint:doc.Fingerprint,Code:outcome.Code),default);
        Assert.Equal(text,(await f.Db.Set<StudyCatalogOutcome>().SingleAsync()).Name);
        var audit=await f.Db.AdminAuditRecords.SingleAsync();Assert.True(audit.ChangedFieldsJson!.Length<=2000);Assert.Contains(reason,audit.ChangedFieldsJson);
    }

    [LocalPostgresFact]
    public async Task StudentCanStillOpenAndCompletePendingTaskAfterItsTopicMoved()
    {
        await using var f=await Pg.Create();var topic=await f.Data.Seed();var plan=await f.Db.StudyPlanRevisions.SingleAsync();
        var pending=StudyPlanTask.Create(plan,new(2026,10,20),"Bekleyen eski çalışma",20,topic.Id,true);f.Db.Add(pending);
        var target=StudyCatalogUnit.Create("test","target",topic.LessonId,"Yeni ünite",2);target.SetActive(true);f.Db.Add(target);await f.Db.SaveChangesAsync();
        var doc=await f.Service.GetAsync("topics",topic.Id,default);await f.Service.MoveAsync("topics",topic.Id,new(target.Id,doc.Fingerprint,"Konuyu taşı"),default);
        f.Data.User.UserId=plan.StudentId;f.Data.User.Roles=["Student"];var access=new CoachingAccessPolicy(f.Data.User);
        var service=new CoachingManualStudyPlanService(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
        var current=(await service.GetAsync(plan.Id))!;Assert.Equal(2,current.Tasks.Count);
        var completed=await service.CompleteTaskAsync(plan.Id,pending.Id,current.Version,18,6);
        var work=completed.Tasks.Single(x=>x.Id==pending.Id);Assert.True(work.IsCompleted);Assert.Equal(18,work.ActualMinutes);Assert.Equal(6,work.ActualQuestions);
        Assert.Equal(topic.Id,work.TopicId);Assert.Equal(25,completed.Tasks.Single(x=>x.Id!=pending.Id).ActualMinutes);
    }

    internal sealed class LocalPostgresFactAttribute : FactAttribute
    { public LocalPostgresFactAttribute() { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("COACHING_CURRICULUM_TEST_POSTGRES"))) Skip = "Set localhost-only COACHING_CURRICULUM_TEST_POSTGRES to run migration tests."; } }
    internal sealed class Pg : IAsyncDisposable
    {
        private readonly NpgsqlConnection admin; private readonly string database;
        public DbContextOptions<CoachingDbContext> Options { get; }
        public CoachingDbContext Db { get; }
        public CurriculumManagementTests.Fixture Data { get; }
        public CoachingCurriculumManagementService Service => Data.Service;
        private Pg(NpgsqlConnection admin,string database,DbContextOptions<CoachingDbContext> options)
        { this.admin=admin;this.database=database;Options=options;Db=new(options);Data=new(Db); }
        public static async Task<Pg> Create(string? targetMigration = null)
        {
            var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("COACHING_CURRICULUM_TEST_POSTGRES")) { Pooling=false };
            Assert.Contains(connection.Host,new[]{"127.0.0.1","localhost"});
            var database="test_coaching_curriculum_"+Guid.NewGuid().ToString("N");
            var admin=new NpgsqlConnection(connection.ConnectionString);await admin.OpenAsync();
            await using(var cmd=new NpgsqlCommand($"CREATE DATABASE \"{database}\"",admin)) await cmd.ExecuteNonQueryAsync();
            connection.Database=database;
            var f=new Pg(admin,database,new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,x => x.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
            if (targetMigration is null) await f.Db.Database.MigrateAsync();
            else await f.Db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync(targetMigration);
            return f;
        }
        public async ValueTask DisposeAsync()
        { await Db.DisposeAsync(); await using(var cmd=new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)",admin)) await cmd.ExecuteNonQueryAsync();await admin.DisposeAsync(); }
    }
}
