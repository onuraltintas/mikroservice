using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class UnifiedTargetCatalogTests
{
    [Fact]
    public void BothCatalogTypesMapToOnePhysicalTable()
    {
        using var db = new Coaching.Infrastructure.Data.CoachingDbContext(
            new DbContextOptionsBuilder<Coaching.Infrastructure.Data.CoachingDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);
        Assert.Equal("target_catalog", db.Model.FindEntityType(typeof(TargetSchool))!.GetTableName());
        Assert.Equal("target_catalog", db.Model.FindEntityType(typeof(TargetUniversityProgram))!.GetTableName());
        Assert.DoesNotContain(db.Model.GetEntityTypes(), x => x.GetTableName() is "target_schools" or "target_university_programs" or "target_admission_batches" or "target_admission_records");
    }

    [Fact]
    public void CatalogAndPlacementYearsStayIndependentAndMissingValuesRemainNull()
    {
        var p = TargetUniversityProgram.Create("code", "University", "Program", null, "Bachelor", "DIL");
        p.UpdateDetails(Row(), 2026, null, null, "https://ogmmateryal.eba.gov.tr/mebi");
        Assert.Equal("YDT", p.ScoreType); Assert.Equal(2026, p.CatalogYear);
        Assert.Null(p.ScoreYear); Assert.Null(p.PlacementStage); Assert.Null(p.Quota);
        Assert.Throws<ArgumentException>(() => p.UpdateDetails(Row() with { Percentile = 101 }, 2026, null, null, "https://meb.gov.tr"));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationCopiesBothCatalogsAndPlacementDetailsWithoutChangingGoalIdentityOrVersion()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create("20261020065330_MebiLgsProgramCatalog");
        var db = pg.Db; var schoolId = Guid.NewGuid(); var programId = Guid.NewGuid();
        var batchId = Guid.NewGuid(); var recordId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.target_schools ("Id","Source","SourceId","Name","City","District","MinimumScore","ScoreYear","IsActive","Language","SchoolType")
            VALUES ({schoolId},'mebi-lgs','school-code','Test Fen Lisesi','ANKARA','ÇANKAYA',450,NULL,TRUE,'İngilizce','Fen Lisesi');
            INSERT INTO coaching.target_university_programs ("Id","UniversityName","Name","ProgramCode","ProgramType","ScoreType","IsActive")
            VALUES ({programId},'Test University','Program','program-code','Bachelor','YDT',TRUE);
            INSERT INTO coaching.target_admission_batches ("Id","ExamCode","Year","Stage","SourceUrl","DocumentHash","RetrievedAt","PublishedAt")
            VALUES ({batchId},'YKS',2026,'Main','https://osym.gov.tr/results',repeat('a',64),now(),now());
            INSERT INTO coaching.target_admission_records ("Id","BatchId","ProgramId","ExternalCode","Institution","Name","ScoreType","Quota","MinimumScore","Rank","MatchStatus")
            VALUES ({recordId},{batchId},{programId},'program-code','Test University','Program','YDT',30,400,12345,'Verified');
            """);
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Korunacak hedef", GoalCategory.ExamPreparation);
        // Seed the historical schema without assuming fields added by later goal migrations.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.academic_goals(id,student_id,title,category,current_progress,is_completed,target_school_id,created_at,"Version")
            VALUES({goal.Id},{goal.StudentId},{goal.Title},'ExamPreparation',0,FALSE,{schoolId},now(),0);
            """);
        var version = 0; DateTime? updated = null;
        await db.GetService<IMigrator>().MigrateAsync(); db.ChangeTracker.Clear();
        var school = await db.TargetSchools.SingleAsync(); var program = await db.TargetUniversityPrograms.SingleAsync();
        Assert.Equal(schoolId, school.Id); Assert.Equal(programId, program.Id);
        Assert.Equal(450, school.MinimumScore); Assert.Null(school.ScoreYear);
        Assert.Equal(30, program.Quota); Assert.Equal(400, program.MinimumScore); Assert.Equal(12345, program.Rank);
        Assert.Equal(2026, program.ScoreYear); Assert.Equal("Main", program.PlacementStage);
        var savedGoal = await db.AcademicGoals.SingleAsync();
        Assert.Equal(schoolId, savedGoal.TargetSchoolId); Assert.Equal(version, savedGoal.Version); Assert.Equal(updated, savedGoal.UpdatedAt);
        Assert.Equal(2, await db.TargetCatalog.CountAsync());
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE coaching.target_catalog SET \"ScoreType\"='LGS' WHERE \"Id\"={programId}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE coaching.target_catalog SET \"Degree\"='Invalid' WHERE \"Id\"={programId}"));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE coaching.target_catalog SET \"ProvinceId\"='06' WHERE \"Id\"={schoolId}"));
        var oldTables = await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='coaching' AND table_name IN ('target_schools','target_university_programs','target_admission_batches','target_admission_records')").SingleAsync();
        Assert.Equal(0, oldTables);
        await db.GetService<IMigrator>().MigrateAsync(); Assert.Equal(2, await db.TargetCatalog.CountAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ImportUpdatesInPlaceWithoutDetachingExistingGoalsAndRejectsInvalidRowsAtomically()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var db = pg.Db;
        var importer = new TargetCatalogImporter(db);
        var package = new TargetCatalogPackage("UniversityProgram", "mebi-yks", "https://ogmmateryal.eba.gov.tr/mebi", 2026, null, null, [Row()]);
        await importer.ImportAsync(package); var program = await db.TargetUniversityPrograms.SingleAsync();
        var goal = AcademicGoal.Create(Guid.NewGuid(), "Mevcut hedef", GoalCategory.ExamPreparation);
        goal.SetCatalogTarget(program.Id, null); db.Add(goal); await db.SaveChangesAsync();
        var repeated = await importer.ImportAsync(package with { Rows = [Row() with { Quota = 40 }] });
        Assert.Equal(0, repeated.Added); Assert.Equal(1, repeated.Updated);
        Assert.Equal(program.Id, (await db.TargetUniversityPrograms.SingleAsync()).Id);
        Assert.Equal(program.Id, (await db.AcademicGoals.SingleAsync()).TargetUniversityProgramId);
        await Assert.ThrowsAsync<ArgumentException>(() => importer.ImportAsync(package with { Rows = [Row() with { Quota = -1 }] }));
        Assert.Equal(40, (await db.TargetUniversityPrograms.SingleAsync()).Quota);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SharedCatalogPreservesTargetOwnershipAndRejectsSelectingWrongCatalogKind()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var db = pg.Db;
        var user = pg.Data.User; user.UserId = Guid.NewGuid(); user.Roles = ["Student"];
        var school = TargetSchool.Create("mebi-lgs", "school", "School", "ANKARA", "ÇANKAYA", null); school.SetActive(true);
        var program = TargetUniversityProgram.Create("program", "University", "Program", null, "Bachelor", "YDT"); program.SetActive(true);
        var goal = AcademicGoal.Create(user.UserId.Value, "Öğrenci hedefi", GoalCategory.ExamPreparation);
        var teacherGoal = AcademicGoal.Create(user.UserId.Value, "Öğretmen hedefi", GoalCategory.ExamPreparation, Guid.NewGuid());
        db.AddRange(school, program, goal, teacherGoal); await db.SaveChangesAsync();
        var service = new Coaching.Infrastructure.StudyPlanning.CoachingGoalTargetService(db,
            new Coaching.Application.Authorization.CoachingAccessPolicy(user), new CurriculumManagementTests.NoopPublisher());
        var initial = (await service.GetAsync(goal.Id))!;
        var linked = await service.ReplaceAsync(goal.Id, new(initial.Version, null, school.Id));
        Assert.Equal(school.Id, linked.TargetSchoolId); Assert.Equal("School", linked.CatalogTarget!.Name);
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(goal.Id, new(linked.Version, null, program.Id)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(goal.Id, new(linked.Version, school.Id, null)));
        var changed = await service.ReplaceAsync(goal.Id, new(linked.Version, program.Id, null));
        Assert.Equal(program.Id, changed.TargetUniversityProgramId); Assert.Null(changed.TargetSchoolId);
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() => service.ReplaceAsync(teacherGoal.Id, new(teacherGoal.Version, program.Id, null)));
    }

    private static TargetAdmissionData Row() => new("code", "University", "Program", "YDT", null,null,null,"Bachelor",null,null,null,null,null,null,null,null,null,null);
}
