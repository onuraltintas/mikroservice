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
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var teacher = Exam.Create(Guid.NewGuid(), "Teacher", ExamType.Mock, DateTime.UtcNow, 100);
            var student = Exam.CreateStudentReported(Guid.NewGuid(), "Student", ExamType.Mock, DateTime.UtcNow, 100);
            db.AddRange(teacher, student); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261002111022_TrackAutomaticStudyPlanSource";
            await Assert.ThrowsAsync<Npgsql.PostgresException>(() => migrator.MigrateAsync(previous));
            Assert.True(await db.Exams.AnyAsync(x => x.Id == student.Id && x.CreatedByTeacherId == null));
            await db.Exams.Where(x => x.Id == student.Id).ExecuteDeleteAsync();
            await migrator.MigrateAsync(previous);
            await migrator.MigrateAsync();
            Assert.Equal(teacher.CreatedByTeacherId, (await db.Exams.SingleAsync()).CreatedByTeacherId);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
