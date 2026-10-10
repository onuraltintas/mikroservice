using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
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
}
