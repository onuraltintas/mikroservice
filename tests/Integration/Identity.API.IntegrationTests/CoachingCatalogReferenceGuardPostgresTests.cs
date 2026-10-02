using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingCatalogReferenceGuardPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ExamJsonCannotReferenceDeletedCatalogAndReferencedCatalogCannotBeDeletedDirectly()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL)");
            await db.Database.ExecuteSqlRawAsync(db.GetService<IMigrator>().GenerateScript(
                "20261002151257_LinkTargetSchoolAdministrativeLocations", "GuardExamCatalogReferences"));
            var lesson = StudyCatalogLesson.Create("admin", "l1", "Math", 8, "LGS");
            var exam = Exam.Create(Guid.NewGuid(), "Exam", ExamType.LGS, DateTime.UtcNow, 500);
            db.AddRange(lesson, exam);
            await db.SaveChangesAsync();
            var result = ExamResult.Create(exam.Id, Guid.NewGuid(), 400);
            result.SetAnswerStatistics(1, 0, 0);
            result.SetLessonAnswers([new(lesson.Id, null, 1, 1, 0, 0)]);
            db.Add(result);
            await db.SaveChangesAsync();
            await using (var writer = await db.Database.BeginTransactionAsync())
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.exam_results SET lesson_answers = lesson_answers WHERE id = {result.Id}");
                await using var concurrent = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
                await using var deleting = await concurrent.Database.BeginTransactionAsync();
                await concurrent.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '150ms'");
                var locked = await Assert.ThrowsAsync<PostgresException>(() => concurrent.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM coaching.study_catalog_lessons WHERE \"Id\" = {lesson.Id}"));
                Assert.Equal("55P03", locked.SqlState);
                await writer.CommitAsync();
            }
            var compactReference = System.Text.Json.JsonSerializer.Serialize(new[] { new { LessonId = lesson.Id.ToString("N"), TopicId = (string?)null } });
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE coaching.exam_results SET lesson_answers = {compactReference}::jsonb WHERE id = {result.Id}");
            var inUse = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM coaching.study_catalog_lessons WHERE \"Id\" = {lesson.Id}"));
            Assert.Equal("23503", inUse.SqlState);
            result.SetLessonAnswers([new(Guid.NewGuid(), null, 1, 1, 0, 0)]);
            var missing = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("23503", Assert.IsType<PostgresException>(missing.InnerException).SqlState);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
