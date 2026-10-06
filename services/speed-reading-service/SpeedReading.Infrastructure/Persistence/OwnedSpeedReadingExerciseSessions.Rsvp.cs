using System.Text.Json;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed partial class OwnedSpeedReadingExerciseSessions
{
    private static bool IsRsvp(SessionState state) =>
        !state.ExerciseTypeName.Equals("Tachistoscope", StringComparison.OrdinalIgnoreCase)
        && ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "text_stream"
        && (state.ExerciseTypeName.Equals("RSVP", StringComparison.OrdinalIgnoreCase)
            || state.TextStreamMode.Equals("rsvp", StringComparison.OrdinalIgnoreCase));

    private static bool IsRsvp(string type, string engine, JsonElement configuration) => IsRsvp(new SessionState {
        ExerciseTypeName = type, EngineType = engine,
        TextStreamMode = ReadString(ReadObject(configuration, "engineConfig"), "mode")
            ?? ReadString(configuration, "mode") ?? string.Empty
    });

    private static void InitializeRsvp(SessionState state, JsonElement root, JsonElement nested)
    {
        if (state.Words.Length == 0)
            throw new InvalidOperationException("RSVP için geçerli bir metin gereklidir.");
        var timing = ReadObject(nested, "timing");
        var rootTiming = ReadObject(root, "timing");
        state.RsvpDisplayDurationMs = Math.Clamp(ReadPositiveInt(nested, "displayDurationMs")
            ?? ReadPositiveInt(nested, "intervalMs") ?? ReadPositiveInt(root, "displayDurationMs")
            ?? ReadPositiveInt(root, "intervalMs") ?? ReadPositiveInt(timing, "durationMs")
            ?? ReadPositiveInt(rootTiming, "durationMs") ?? 500, 50, 5000);
        state.RsvpGapMs = Math.Clamp(ReadNonNegativeInt(timing, "intervalMs")
            ?? ReadNonNegativeInt(rootTiming, "intervalMs") ?? 0, 0, 10000);
        var fixation = ReadProperty(ReadObject(nested, "visuals"), "showFixation");
        if (fixation.ValueKind == JsonValueKind.Undefined)
            fixation = ReadProperty(ReadObject(root, "visuals"), "showFixation");
        state.RsvpFixationMs = fixation.ValueKind == JsonValueKind.False ? 0 : 300;
        state.WordCount = state.Words.Length;
        state.TotalSteps = state.WordCount;
        state.ReadingMinimumMs = checked(state.WordCount * (state.RsvpDisplayDurationMs + state.RsvpFixationMs)
            + (state.WordCount - 1) * state.RsvpGapMs);
        state.RsvpDisplayPaceWpm = Math.Round(state.WordCount * 60000m / state.ReadingMinimumMs, 2);
        state.ReadingPausedMilliseconds = 0;
        state.RsvpProtocolVersion = 1;
    }
}
