using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStudyAvailabilityPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Availability_RoundTripsWindowsAndRejectsDuplicateOrStalePreferences()
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new CoachingDbContext(options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE \"__EFMigrationsHistory\" (\"MigrationId\" varchar(150) PRIMARY KEY, \"ProductVersion\" varchar(32) NOT NULL);");
            var migrator = db.GetService<IMigrator>();
            const string previous = "20261002093428_AddStudentStudyPlans";
            const string current = "20261002093801_AddStudyAvailability";
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(current, previous));
            await db.Database.ExecuteSqlRawAsync(migrator.GenerateScript(previous, current));
            var student = Guid.NewGuid();
            var preferences = StudyAvailability.Create(student, "Europe/Istanbul");
            preferences.ReplaceWindows([new(DayOfWeek.Monday, 480, 540)]);
            db.StudyAvailability.Add(preferences);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            db.StudyAvailability.Add(StudyAvailability.Create(student, "Europe/Istanbul"));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            var first = await db.StudyAvailability.SingleAsync();
            Assert.Equal(480, first.Windows.Single().StartMinute);
            await using var otherDb = new CoachingDbContext(options);
            var stale = await otherDb.StudyAvailability.SingleAsync();
            first.ReplaceWindows([new(DayOfWeek.Tuesday, 600, 660)]);
            await db.SaveChangesAsync();
            stale.ReplaceWindows([]);
            await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.ConcurrencyException>(() => otherDb.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal(DayOfWeek.Tuesday, (await db.StudyAvailability.SingleAsync()).Windows.Single().Day);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
