using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// "Hedef Avı" (decision 29.10.2026): the server builds the layout, validates every tap against it, owns the clock and
/// scores found targets minus wrong taps. The layout itself is public; the student is meant to see every item.
/// </summary>
internal sealed partial class OwnedSpeedReadingExerciseSessions
{
    private static bool IsTargetHunt(SessionState state) =>
        !string.IsNullOrWhiteSpace(state.EngineType)
        && NormalizeTargetHuntEngine(state.EngineType) == TargetHuntRules.EngineType;

    private static string? NormalizeTargetHuntEngine(string engineType)
    {
        try { return ExerciseConfigurationRules.NormalizeEngineType(engineType); }
        catch (ArgumentException) { return null; }
    }

    private static void InitializeTargetHunt(SessionState state, JsonElement config)
    {
        TargetHuntSettings settings;
        try { settings = TargetHuntRules.Read(config); }
        catch (ArgumentException exception)
        {
            throw new BusinessRuleException("ExerciseSession.TargetHuntContentInvalid",
                $"Hedef avı ayarları geçersiz: {exception.Message}");
        }
        state.TargetHuntTarget = settings.Target;
        state.TargetHuntColumns = settings.Columns;
        state.TargetHuntItems = TargetHuntRules.CreateLayout(settings, Random.Shared);
        state.TargetHuntHits = [];
        state.TargetHuntFalseTaps = [];
        state.TargetHuntCompleted = false;
        state.TargetHuntElapsedMs = 0;
        state.TotalSteps = settings.TargetCount;
        state.TimeLimitSeconds = settings.TimeLimitSeconds;
        state.Questions.Clear();
        state.ReadingPausedMilliseconds = 0;
    }

    private static decimal TargetHuntAccuracy(SessionState state) =>
        TargetHuntRules.Accuracy(state.TargetHuntHits.Count, state.TargetHuntFalseTaps.Count, state.TotalSteps);

    private static decimal TargetHuntScore(SessionState state) =>
        TargetHuntRules.Score(state.TargetHuntHits.Count, state.TargetHuntFalseTaps.Count, state.TotalSteps);

    private static ExerciseActionValidationResponse ValidateTargetHunt(ExerciseSession session, SessionState state,
        ExerciseActionRequest request, DateTime now)
    {
        if (state.TargetHuntItems.Length == 0 || state.TotalSteps == 0) return Invalid("Egzersizi yeniden başlatın.");
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("target_hunt_start" or "target_hunt_tap" or "target_hunt_finish"))
            return Invalid("Hedef avı yalnız doğrulanmış dokunuşlarla ilerler.");
        if (state.TargetHuntCompleted) return Invalid("Hedef avı zaten tamamlandı.");
        if (action == "target_hunt_start") EnsureTimingStarted(session, state, now);
        if (!state.TimingStartedAt.HasValue) return Invalid("Önce egzersizi başlatın.");
        var limitMs = (state.TimeLimitSeconds ?? 60) * 1000L;
        var elapsed = ErrorAnalysisActiveMs(state, now);
        bool? correct = null;
        if (elapsed >= limitMs || action == "target_hunt_finish")
            state.TargetHuntCompleted = true;
        else if (action == "target_hunt_tap")
        {
            if (request.Index is not { } index || index < 0 || index >= state.TargetHuntItems.Length)
                return Invalid("Geçersiz konum.");
            if (state.TargetHuntHits.Contains(index) || state.TargetHuntFalseTaps.Contains(index))
                return Invalid("Bu öğeye zaten dokundunuz.");
            correct = state.TargetHuntItems[index] == state.TargetHuntTarget;
            if (correct.Value) { state.TargetHuntHits.Add(index); session.Advance(); }
            else { state.TargetHuntFalseTaps.Add(index); session.RecordIncorrectAttempt(); }
            state.TargetHuntCompleted = state.TargetHuntHits.Count == state.TotalSteps;
        }
        state.TargetHuntElapsedMs = Math.Min(elapsed, limitMs);
        return Valid(correct switch { true => "Doğru!", false => "Bu hedef değil.", null => "Hedef avı doğrulandı." },
            nextStep: session.CurrentStep, isCompleted: state.TargetHuntCompleted, isCorrect: correct,
            feedbackData: JsonSerializer.SerializeToElement(new
            {
                hits = state.TargetHuntHits, falseTaps = state.TargetHuntFalseTaps, timeElapsed = state.TargetHuntElapsedMs,
                totalTargets = state.TotalSteps, accuracy = TargetHuntAccuracy(state), score = TargetHuntScore(state)
            }, JsonOptions));
    }

    private sealed partial class SessionState
    {
        public string[] TargetHuntItems { get; set; } = [];
        public string TargetHuntTarget { get; set; } = string.Empty;
        public int TargetHuntColumns { get; set; }
        public List<int> TargetHuntHits { get; set; } = [];
        public List<int> TargetHuntFalseTaps { get; set; } = [];
        public bool TargetHuntCompleted { get; set; }
        public long TargetHuntElapsedMs { get; set; }
    }
}
