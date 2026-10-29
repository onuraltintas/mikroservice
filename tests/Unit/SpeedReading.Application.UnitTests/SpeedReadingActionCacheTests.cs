using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H4 (finding 22): the per-session replay cache for exercise actions keeps only the latest answers, so a long
/// exercise no longer rewrites an ever-growing JSON row on every step. Retries of recent actions still replay.
/// </summary>
public sealed class SpeedReadingActionCacheTests
{
    [Fact]
    public void The_action_cache_keeps_only_the_most_recent_actions()
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!;
        var record = type.GetMethod("RecordCachedAction", BindingFlags.NonPublic | BindingFlags.Static)!;
        var json = "{}";
        var ids = Enumerable.Range(0, 120).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in ids)
            json = (string)record.Invoke(null, [json, id, new ExerciseActionValidationResponse(true, "ok", null, null, null, false, null, null, null, null, null)])!;

        var kept = JsonSerializer.Deserialize<Dictionary<Guid, JsonElement>>(json)!;
        kept.Count.Should().BeLessThanOrEqualTo(50);
        kept.Keys.Should().Contain(ids[^1]).And.Contain(ids[^50]).And.NotContain(ids[0]);
    }
}
