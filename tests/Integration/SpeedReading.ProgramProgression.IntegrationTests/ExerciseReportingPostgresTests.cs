using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

public sealed class ExerciseReportingPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private OwnedSpeedReadingDbContext Open() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseNpgsql(postgres.ConnectionString, b => b.MigrationsHistoryTable("__ef_migrations_history", "speed_reading")).Options);

    [Theory]
    [InlineData("archived")]
    [InlineData("boundary")]
    [InlineData("calendar")]
    [InlineData("success")]
    public async Task Report_average_audit_preserves_history_periods_calendar_and_real_zero(string mode)
    {
        await using var db = Open(); await Reset(db);
        await ReportAverageAuditScenario.Run(db, mode);
    }

    [Fact]
    public async Task Institution_benchmark_uses_active_student_memberships_and_matches_overview()
    {
        await using var db = Open(); await Reset(db);
        await InstitutionBenchmarkScenario.Run(db);
    }

    [Theory]
    [InlineData("multi")]
    [InlineData("zero")]
    [InlineData("absent")]
    public async Task Institution_benchmarks_preserve_scope_missing_data_and_real_zero(string mode)
    {
        await using var db = Open(); await Reset(db);
        await InstitutionBenchmarkScenario.Boundaries(db, mode);
    }

    [Fact]
    public async Task All_exercise_types_reach_student_teacher_and_institution_without_duplicate_readings()
    {
        await using var db = Open(); await Reset(db);
        await ExerciseReportingScenario.Run(db);
    }

    [Fact]
    public async Task Activity_and_series_queries_load_saved_reading_exercise_and_program_ids_on_postgres()
    {
        await using var db = Open(); await Reset(db);
        var user = Guid.NewGuid(); var now = DateTime.UtcNow;
        var type = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Kelime", "vocabulary_builder");
        var exercise = Exercise.Create("Kelime testi", "vocabulary_builder", "{}", 1, user, type.Id);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100,
            "{}", 1, 2, 5, 1, 3, true, 1, 0, null, false, now, null, null, null);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now);
        db.ProgramTemplates.Add(template); db.StudentProgramProgresses.Add(progress);
        db.DailyExerciseLogs.Add(ExerciseReportingScenario.Log(user, progress.Id, exercise, now, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var analytics = ExerciseReportingScenario.Analytics(db);
        Assert.Single((await analytics.GetStudentActivityAsync(user, now.AddDays(-1), now.AddDays(1))).RecentActivities);
        Assert.NotEmpty((await analytics.GetStudentSeriesAsync(user, now.AddDays(-1), now.AddDays(1))).ActiveSeries);
    }

    private static async Task Reset(OwnedSpeedReadingDbContext db)
    {
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("unmeasured")]
    [InlineData("zero")]
    [InlineData("measured")]
    [InlineData("comprehension-only")]
    public async Task Missing_measurements_do_not_generate_performance_labels_or_advice(string mode)
    {
        await using var db = Open(); await Reset(db);
        await UnmeasuredReportScenario.Run(db, mode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Exercise_pass_threshold_and_type_identity_are_preserved(bool sameDisplayName)
    {
        await using var db = Open(); await Reset(db);
        await ExerciseReportingScenario.Boundaries(db, sameDisplayName);
    }

    [Fact]
    public async Task Legacy_archived_and_adaptive_results_preserve_history_and_measurements()
    {
        await using var db = Open(); await Reset(db);
        await ExerciseReportingScenario.LegacyAndAdaptive(db);
    }
}
