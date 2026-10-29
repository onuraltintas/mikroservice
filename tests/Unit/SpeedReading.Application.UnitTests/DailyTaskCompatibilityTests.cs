using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Application.Progress;

namespace SpeedReading.Application.UnitTests;

public sealed class DailyTaskCompatibilityTests
{
    [Fact]
    public void Legacy_completion_retry_keeps_the_pre_slot_request_hash()
    {
        var user = Guid.NewGuid();
        var request = new CompleteDailyExerciseRequest { ExerciseId = Guid.NewGuid(), SessionId = Guid.NewGuid() };
        var oldPayload = JsonNode.Parse(JsonSerializer.Serialize(request))!.AsObject();
        oldPayload.Remove("SlotOrder");
        oldPayload.Remove("ProgramProgressId");
        oldPayload.Remove("ProgramDay");
        var expected = SpeedReadingRequestHasher.Create(user.ToString("D"),
            "speed-reading.daily-progress.complete-exercise", oldPayload.ToJsonString());
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!;
        var actual = type.GetMethod("CreateCompletionHash", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [user, request]);
        actual.Should().Be(expected);
    }

    [Fact]
    public async Task Completing_a_daily_task_without_an_active_program_is_a_business_rule()
    {
        // Review H1: an expected state answers 4xx with a Turkish message instead of a 500.
        await using var db = new OwnedSpeedReadingDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingDailyProgress")!;
        var service = (SpeedReading.Application.DailyProgress.ISpeedReadingDailyProgress)Activator.CreateInstance(
            type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db, null], null)!;
        var complete = () => service.CompleteExerciseAsync(Guid.NewGuid(),
            new SpeedReading.Application.DailyProgress.CompleteDailyExerciseRequest { ExerciseId = Guid.NewGuid(), SessionId = Guid.NewGuid() },
            "daily-complete-key-0001");
        (await complete.Should().ThrowAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>())
            .Which.Code.Should().Be("DailyProgress.NoActiveProgram");
    }
}
