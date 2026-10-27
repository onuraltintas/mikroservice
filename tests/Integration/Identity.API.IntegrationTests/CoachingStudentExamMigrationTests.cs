using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudentExamMigrationTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Migration_RoundTripsTeacherDataAndRefusesDestructiveStudentRollback()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        const string ownershipMigration = "20261002113458_AddStudentReportedExamOwnership";
        await db.GetService<IMigrator>().MigrateAsync(ownershipMigration);
        // Exam rows are written and read on the older schema without the columns added later (roadmap O4).
        await using var data = new Coaching.Curriculum.IntegrationTests.LegacyExamSchemaDbContext(postgres.ConnectionString);
        try
        {
            var teacher = Exam.Create(Guid.NewGuid(), "Teacher", ExamType.Mock, DateTime.UtcNow, 100);
            var student = Exam.CreateStudentReported(Guid.NewGuid(), "Student", ExamType.Mock, DateTime.UtcNow, 100);
            data.AddRange(teacher, student); await data.SaveChangesAsync(); data.ChangeTracker.Clear();
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261002111022_TrackAutomaticStudyPlanSource";
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync(previous));
            Assert.True(await data.Exams.AnyAsync(x => x.Id == student.Id && x.CreatedByTeacherId == null));
            await data.Exams.Where(x => x.Id == student.Id).ExecuteDeleteAsync();
            await migrator.MigrateAsync(previous);
            await migrator.MigrateAsync(ownershipMigration);
            Assert.Equal(teacher.CreatedByTeacherId, (await data.Exams.SingleAsync()).CreatedByTeacherId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
