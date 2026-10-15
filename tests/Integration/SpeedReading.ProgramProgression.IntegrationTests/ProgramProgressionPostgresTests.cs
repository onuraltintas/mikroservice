using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

public sealed class ProgramProgressionPostgresTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private OwnedSpeedReadingDbContext Open(bool failOnce = false)
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString, builder => builder.EnableRetryOnFailure(2, TimeSpan.Zero, null));
        if (failOnce) options.AddInterceptors(new FailOnceAfterSave());
        return new(options.Options);
    }

    internal static ISpeedReadingProgramProgression Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingProgramProgression)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramProgression")!, db, new Eligibility())!;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Concurrent_requests_and_transaction_retry_grant_one_day_without_changing_history(bool failOnce)
    {
        await using var setup = Open();
        await setup.Database.EnsureDeletedAsync();
        await setup.Database.MigrateAsync();
        Assert.False(setup.Database.HasPendingModelChanges());
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var type = Guid.NewGuid();
        setup.ExerciseTypes.Add(ExerciseType.Create(type, "Fixation", "Fixation", "focus"));
        var exerciseEntity = Exercise.Create("Exercise", "Fixation", "{}", 1, user, type);
        setup.Exercises.Add(exerciseEntity);
        var exercise = exerciseEntity.Id;
        var template = ProgramTemplate.Import(Guid.NewGuid(), "Progression test", "", Guid.NewGuid(), 0, 100,
            "{}", 1, 2, 5, 1, 3, true, 1, 0, null, false, now, null, null, null);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now);
        progress.SetSchedule(JsonSerializer.Serialize(new[] {
            new { WeekNumber = 1, DayNumber = 1, Order = 1, ExerciseId = exercise },
            new { WeekNumber = 1, DayNumber = 1, Order = 2, ExerciseId = exercise },
            new { WeekNumber = 1, DayNumber = 2, Order = 1, ExerciseId = exercise }
        }), user, now);
        progress.ApplyExerciseCompletion(80, false, 1, 2, template, user, now);
        progress.ApplyExerciseCompletion(80, false, 2, 2, template, user, now);
        setup.ProgramTemplates.Add(template);
        setup.StudentProgramProgresses.Add(progress);
        for (var slot = 1; slot <= 2; slot++) setup.DailyExerciseLogs.Add(DailyExerciseLog.Import(
            Guid.NewGuid(), user, progress.Id, exercise, type, 1, 1, 1, now, 60, 80, true, "{}", 1, false,
            "test", 1, 0, 1, null, null, 4, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0,
            now, null, null, null, slotOrder: slot));
        await setup.SaveChangesAsync();
        await Service(setup).UpdateSettingsAsync(false, user);
        await Service(setup).UpdatePreferenceAsync(user, "allow", user);
        var logs = await setup.DailyExerciseLogs.AsNoTracking().OrderBy(item => item.Id).ToListAsync();
        var before = JsonSerializer.Serialize(logs);
        var assigned = (await setup.StudentProgramProgresses.AsNoTracking().SingleAsync()).AssignedDate;
        var request = new AdvanceProgramDayRequest(progress.Id, 1);
        async Task Advance(bool retry)
        {
            await using var db = Open(retry);
            await Service(db).AdvanceAsync(user, request);
        }
        await Task.WhenAll(Advance(failOnce), Advance(false));
        await Advance(false);
        setup.ChangeTracker.Clear();
        var saved = await setup.StudentProgramProgresses.SingleAsync();
        Assert.Equal(1, saved.EarlyAdvanceDays);
        Assert.Equal(2, saved.CurrentDay);
        Assert.Equal(1, saved.DaysCompleted);
        Assert.Equal(2, saved.ExercisesCompleted);
        Assert.Equal(assigned, saved.AssignedDate);
        Assert.Equal(before, JsonSerializer.Serialize(await setup.DailyExerciseLogs.AsNoTracking().OrderBy(item => item.Id).ToListAsync()));
        await Service(setup).UpdatePreferenceAsync(user, "deny", user);
        Assert.False((await Service(setup).GetAdvanceStateAsync(user)).AllowEarlyAdvance);
        Assert.Equal(1, (await setup.StudentProgramProgresses.AsNoTracking().SingleAsync()).EarlyAdvanceDays);
    }

    private sealed class Eligibility : ISpeedReadingInstitutionMemberEligibility
    {
        public Task<bool> IsEligibleAsync(Guid id, SpeedReadingInstitutionMemberRole role, CancellationToken token = default) => Task.FromResult(true);
    }

    private sealed class FailOnceAfterSave : SaveChangesInterceptor
    {
        private bool failed;
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (!failed)
            {
                failed = true;
                throw new NpgsqlException("Injected transient failure before commit", new TimeoutException());
            }
            return ValueTask.FromResult(result);
        }
    }
}
