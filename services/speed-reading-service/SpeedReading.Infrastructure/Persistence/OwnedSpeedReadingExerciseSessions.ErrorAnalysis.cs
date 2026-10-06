using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed partial class OwnedSpeedReadingExerciseSessions
{
    private static bool IsErrorAnalysis(SessionState state) => ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "error_analysis";

    private static void InitializeErrorAnalysis(SessionState state, JsonElement config)
    {
        var words = ReadProperty(config, "words"); var errors = ReadProperty(config, "errors");
        if (words.ValueKind != JsonValueKind.Array || errors.ValueKind != JsonValueKind.Array
            || words.GetArrayLength() is < 1 or > 10000 || errors.GetArrayLength() is < 1 or > 1000)
            throw ErrorAnalysisContentError();
        foreach (var word in words.EnumerateArray())
        {
            var index = ReadNonNegativeInt(word, "index"); var text = ReadString(word, "text");
            if (!index.HasValue || string.IsNullOrWhiteSpace(text) || text.Length > 1000
                || state.ErrorAnalysisWords.Any(item => item.Index == index)) throw ErrorAnalysisContentError();
            state.ErrorAnalysisWords.Add(new() { Index = index.Value, Text = text });
        }
        foreach (var error in errors.EnumerateArray())
        {
            var index = ReadNonNegativeInt(error, "wordIndex");
            var original = ReadString(error, "originalWord"); var incorrect = ReadString(error, "errorWord");
            if (!index.HasValue || string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(incorrect)
                || original == incorrect || original.Length > 1000
                || state.ErrorAnalysisWords.SingleOrDefault(word => word.Index == index)?.Text != incorrect
                || state.ErrorAnalysisErrors.Any(item => item.WordIndex == index)) throw ErrorAnalysisContentError();
            state.ErrorAnalysisErrors.Add(new() { WordIndex = index.Value, OriginalWord = original, ErrorWord = incorrect,
                Explanation = ReadString(error, "explanation") ?? "", ErrorType = ReadString(error, "errorType") ?? "spelling" });
        }
        state.TotalSteps = state.ErrorAnalysisErrors.Count;
        state.Questions.Clear(); state.ReadingPausedMilliseconds = 0;
        state.TimeLimitSeconds = Math.Clamp(ReadGridTimeLimit(config) ?? 180, 1, 3600);
    }

    private static BusinessRuleException ErrorAnalysisContentError() => new("ExerciseSession.ErrorAnalysisContentInvalid",
        "Hata analizi içeriği eksik veya tutarsız. Lütfen farklı bir egzersiz seçin.");

    private static long ErrorAnalysisActiveMs(SessionState state, DateTime now) => state.TimingStartedAt.HasValue
        ? (long)Math.Max(0, ((state.ReadingPausedAt ?? now) - state.TimingStartedAt.Value).TotalMilliseconds
            - (state.ReadingPausedMilliseconds ?? 0)) : 0;

    private static decimal ErrorAnalysisAccuracy(SessionState state) => state.TotalSteps > 0
        ? Math.Round(100m * state.ErrorAnalysisFound.Count / (state.TotalSteps + state.ErrorAnalysisFalseAlarms.Count), 2) : 0;

    private static decimal ErrorAnalysisScore(SessionState state) => state.TotalSteps > 0
        ? Math.Max(0, Math.Round(100m * state.ErrorAnalysisFound.Count / state.TotalSteps
            - Math.Min(state.ErrorAnalysisFalseAlarms.Count * 5, 30), 2)) : 0;

    private static ExerciseActionValidationResponse ValidateErrorAnalysis(ExerciseSession session, SessionState state,
        ExerciseActionRequest request, DateTime now)
    {
        if (state.ErrorAnalysisWords.Count == 0 || state.ErrorAnalysisErrors.Count == 0) return Invalid("Egzersizi yeniden başlatın.");
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("error_analysis_start" or "error_analysis_select" or "error_analysis_hint" or "error_analysis_finish"))
            return Invalid("Hata analizi yalnız doğrulanmış kelime seçimleriyle ilerler.");
        if (state.ErrorAnalysisCompleted) return Invalid("Hata analizi zaten tamamlandı.");
        if (action == "error_analysis_start") EnsureTimingStarted(session, state, now);
        if (!state.TimingStartedAt.HasValue) return Invalid("Önce egzersizi başlatın.");
        var elapsed = ErrorAnalysisActiveMs(state, now);
        var expired = elapsed >= state.TimeLimitSeconds * 1000L;
        bool? correct = null; int? hintIndex = null; string? explanation = null;
        if (expired || action == "error_analysis_finish") state.ErrorAnalysisCompleted = true;
        else if (action == "error_analysis_select")
        {
            if (request.Index is not { } index || !state.ErrorAnalysisWords.Any(word => word.Index == index)) return Invalid("Geçersiz kelime konumu.");
            if (!state.ErrorAnalysisSelected.Add(index)) return Invalid("Bu kelime zaten seçildi.");
            var error = state.ErrorAnalysisErrors.SingleOrDefault(item => item.WordIndex == index);
            correct = error is not null;
            if (correct.Value) { state.ErrorAnalysisFound.Add(index); session.Advance(); explanation = error!.Explanation.Length > 0 ? error.Explanation : $"Doğru yazılış: {error.OriginalWord}"; }
            else { state.ErrorAnalysisFalseAlarms.Add(index); session.RecordIncorrectAttempt(); explanation = "Bu kelimede hata yok."; }
            state.ErrorAnalysisCompleted = state.ErrorAnalysisFound.Count == state.TotalSteps;
        }
        else if (action == "error_analysis_hint")
        {
            hintIndex = state.ErrorAnalysisErrors.FirstOrDefault(error => !state.ErrorAnalysisFound.Contains(error.WordIndex))?.WordIndex;
            if (hintIndex.HasValue) state.ErrorAnalysisHints++;
        }
        state.ErrorAnalysisElapsedMs = Math.Min(elapsed, (state.TimeLimitSeconds ?? 180) * 1000L);
        return Valid("Hata analizi doğrulandı.", nextStep: session.CurrentStep, isCompleted: state.ErrorAnalysisCompleted,
            isCorrect: correct, feedbackData: JsonSerializer.SerializeToElement(new {
                selected = state.ErrorAnalysisSelected, found = state.ErrorAnalysisFound, falseAlarms = state.ErrorAnalysisFalseAlarms,
                hintIndex, explanation, hintUsedCount = state.ErrorAnalysisHints, timeElapsed = state.ErrorAnalysisElapsedMs,
                accuracy = ErrorAnalysisAccuracy(state), score = ErrorAnalysisScore(state), totalErrors = state.TotalSteps,
                missedErrors = state.TotalSteps - state.ErrorAnalysisFound.Count,
                errors = state.ErrorAnalysisCompleted ? state.ErrorAnalysisErrors : null
            }, JsonOptions));
    }

    private sealed class ErrorAnalysisWord { public int Index { get; set; } public string Text { get; set; } = ""; }
    private sealed class ErrorAnalysisError
    {
        public int WordIndex { get; set; } public string OriginalWord { get; set; } = ""; public string ErrorWord { get; set; } = "";
        public string Explanation { get; set; } = ""; public string ErrorType { get; set; } = "spelling";
    }
}
