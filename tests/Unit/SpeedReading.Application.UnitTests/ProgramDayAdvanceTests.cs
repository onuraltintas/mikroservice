using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;
using EduPlatform.Shared.Contracts.Events.Privacy;

namespace SpeedReading.Application.UnitTests;

public sealed class ProgramDayAdvanceTests
{
    [Fact]
    public void Early_days_preserve_calendar_and_keep_advancing_after_permission_is_disabled()
    {
        var at = new DateTime(2026, 10, 15, 20, 0, 0, DateTimeKind.Utc);
        SpeedReadingDailyProgressRules.GetAvailableDay(at, at, false, 2).Should().Be(3);
        SpeedReadingDailyProgressRules.GetAvailableDay(at, at.AddHours(1), false, 2).Should().Be(4);
        SpeedReadingDailyProgressRules.GetAvailableDay(at, at, true, 2).Should().Be(int.MaxValue);
    }

    [Theory]
    [InlineData(false, "inherit", false)]
    [InlineData(true, "inherit", true)]
    [InlineData(false, "allow", true)]
    [InlineData(true, "allow", true)]
    [InlineData(false, "deny", false)]
    [InlineData(true, "deny", false)]
    public async Task User_preference_has_priority_over_the_default(bool global, string mode, bool allowed)
    {
        await using var fixture = await Fixture.Create();
        await fixture.Service.UpdateSettingsAsync(global, fixture.Actor);
        var preference = await fixture.Service.UpdatePreferenceAsync(fixture.User, mode, fixture.Actor);
        preference.AllowEarlyAdvance.Should().Be(allowed);
        preference.Mode.Should().Be(mode);
        (await fixture.Service.GetAdvanceStateAsync(fixture.User)).CanAdvance.Should().Be(allowed);
    }

    [Fact]
    public async Task Default_is_closed_and_cannot_be_bypassed_by_calling_the_endpoint()
    {
        await using var fixture = await Fixture.Create();
        (await fixture.Service.GetSettingsAsync()).AllowEarlyAdvance.Should().BeFalse();
        await FluentActions.Awaiting(() => fixture.Service.AdvanceAsync(fixture.User,
            new(fixture.Progress.Id, 1))).Should().ThrowAsync<BusinessRuleException>();
        fixture.Progress.EarlyAdvanceDays.Should().Be(0);
    }

    [Fact]
    public async Task Advance_is_idempotent_and_does_not_change_results_or_completed_counts()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Service.UpdatePreferenceAsync(fixture.User, "allow", fixture.Actor);
        var assignedAt = fixture.Progress.AssignedDate;
        var completed = fixture.Progress.ExercisesCompleted;
        var lastCompletion = fixture.Progress.LastCompletionDate;
        var request = new AdvanceProgramDayRequest(fixture.Progress.Id, 1);
        await fixture.Service.AdvanceAsync(fixture.User, request);
        await fixture.Service.AdvanceAsync(fixture.User, request);
        fixture.Progress.EarlyAdvanceDays.Should().Be(1);
        fixture.Progress.CurrentDay.Should().Be(2);
        fixture.Progress.DaysCompleted.Should().Be(1);
        fixture.Progress.ExercisesCompleted.Should().Be(completed);
        fixture.Progress.AssignedDate.Should().Be(assignedAt);
        fixture.Progress.LastCompletionDate.Should().Be(lastCompletion);
        (await fixture.Db.DailyExerciseLogs.CountAsync()).Should().Be(2);
        (await fixture.Service.GetAdvanceStateAsync(fixture.User)).CanAdvance.Should().BeFalse();
        await fixture.Service.UpdatePreferenceAsync(fixture.User, "deny", fixture.Actor);
        fixture.Progress.EarlyAdvanceDays.Should().Be(1);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task Every_repeated_slot_must_be_completed(int logs, bool ready)
    {
        await using var fixture = await Fixture.Create(logs);
        await fixture.Service.UpdatePreferenceAsync(fixture.User, "allow", fixture.Actor);
        (await fixture.Service.GetAdvanceStateAsync(fixture.User)).CanAdvance.Should().Be(ready);
        if (!ready) await FluentActions.Awaiting(() => fixture.Service.AdvanceAsync(fixture.User,
            new(fixture.Progress.Id, 1))).Should().ThrowAsync<BusinessRuleException>();
    }

    [Fact]
    public async Task Old_or_other_users_program_and_stale_day_cannot_unlock_another_day()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Service.UpdatePreferenceAsync(fixture.User, "allow", fixture.Actor);
        foreach (var request in new[] { new AdvanceProgramDayRequest(Guid.NewGuid(), 1), new(fixture.Progress.Id, 2) })
            await FluentActions.Awaiting(() => fixture.Service.AdvanceAsync(fixture.User, request))
                .Should().ThrowAsync<BusinessRuleException>();
        fixture.Progress.EarlyAdvanceDays.Should().Be(0);
    }

    [Fact]
    public async Task Invalid_preference_and_ineligible_accounts_are_rejected()
    {
        await using var fixture = await Fixture.Create();
        await FluentActions.Awaiting(() => fixture.Service.UpdatePreferenceAsync(fixture.User, "anything", fixture.Actor))
            .Should().ThrowAsync<ArgumentException>();
        fixture.Eligibility.Allowed = false;
        await FluentActions.Awaiting(() => fixture.Service.UpdatePreferenceAsync(fixture.User, "allow", fixture.Actor))
            .Should().ThrowAsync<BusinessRuleException>();
    }

    private sealed class Eligibility : ISpeedReadingInstitutionMemberEligibility
    {
        public bool Allowed = true;
        public Task<bool> IsEligibleAsync(Guid userId, SpeedReadingInstitutionMemberRole role, CancellationToken token = default) => Task.FromResult(Allowed);
    }

    [Fact]
    public async Task User_erasure_includes_preferences_and_keeps_the_global_default_and_other_users()
    {
        await using var fixture = await Fixture.Create();
        await fixture.Service.UpdateSettingsAsync(true, fixture.Actor);
        await fixture.Service.UpdatePreferenceAsync(fixture.User, "allow", fixture.Actor);
        var other = Guid.NewGuid();
        await fixture.Service.UpdatePreferenceAsync(other, "deny", fixture.Actor);
        var inventory = new SpeedReadingPrivacyInventoryRepository(fixture.Db);
        (await inventory.CountByUserAsync(fixture.User, default)).GetValueOrDefault("programProgressionPreferences").Should().Be(1);
        var erasure = new SpeedReadingErasureExecutionService(fixture.Db, inventory, TimeProvider.System);
        var requestId = Guid.NewGuid();
        await erasure.ExecuteAsync(new PersonalDataErasureExecutionRequestedV1(requestId, requestId, fixture.User,
            DateTime.UtcNow, PersonalDataScope.SpeedReading), default);
        (await fixture.Db.ProgramProgressionPolicies.AnyAsync(item => item.UserId == fixture.User)).Should().BeFalse();
        (await fixture.Service.GetSettingsAsync()).AllowEarlyAdvance.Should().BeTrue();
        (await fixture.Service.GetPreferenceAsync(other)).Mode.Should().Be("deny");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public OwnedSpeedReadingDbContext Db = null!;
        public ISpeedReadingProgramProgression Service = null!;
        public Guid User = Guid.NewGuid(), Actor = Guid.NewGuid();
        public StudentProgramProgress Progress = null!;
        public Eligibility Eligibility = new();

        public static async Task<Fixture> Create(int logs = 2)
        {
            var fixture = new Fixture();
            fixture.Db = new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var now = DateTime.UtcNow;
            var type = Guid.NewGuid();
            var exercise = Guid.NewGuid();
            var template = ProgramTemplate.Import(Guid.NewGuid(), "Test", "", Guid.NewGuid(), 0, 100,
                "{}", 1, 2, 5, 1, 3, true, 1, 0, null, false, now, null, null, null);
            fixture.Progress = StudentProgramProgress.Start(Guid.NewGuid(), fixture.User, template, 0, 0, fixture.User, now);
            fixture.Progress.SetSchedule(System.Text.Json.JsonSerializer.Serialize(new[] {
                new { WeekNumber = 1, DayNumber = 1, Order = 1, ExerciseId = exercise },
                new { WeekNumber = 1, DayNumber = 1, Order = 2, ExerciseId = exercise },
                new { WeekNumber = 1, DayNumber = 2, Order = 1, ExerciseId = exercise },
                new { WeekNumber = 1, DayNumber = 3, Order = 1, ExerciseId = exercise }
            }), fixture.User, now);
            fixture.Progress.ApplyExerciseCompletion(80, false, 1, 2, template, fixture.User, now);
            fixture.Progress.ApplyExerciseCompletion(80, false, 2, 2, template, fixture.User, now);
            fixture.Db.ProgramTemplates.Add(template);
            fixture.Db.StudentProgramProgresses.Add(fixture.Progress);
            for (var slot = 1; slot <= logs; slot++) fixture.Db.DailyExerciseLogs.Add(DailyExerciseLog.Import(
                Guid.NewGuid(), fixture.User, fixture.Progress.Id, exercise, type, 1, 1, 1, now, 60, 80, true, "{}", 1, false,
                "test", 1, 0, 1, null, null, 4, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0,
                now, null, null, null, slotOrder: slot));
            await fixture.Db.SaveChangesAsync();
            fixture.Service = (ISpeedReadingProgramProgression)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
                .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramProgression")!, fixture.Db, fixture.Eligibility)!;
            return fixture;
        }

        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
