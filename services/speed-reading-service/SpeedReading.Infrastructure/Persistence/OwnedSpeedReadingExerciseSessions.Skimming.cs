using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed partial class OwnedSpeedReadingExerciseSessions
{
    private static bool IsSkimming(SessionState state) =>
        ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) == "skimming";

    private static void InitializeSkimming(SessionState state, JsonElement config, JsonElement root)
    {
        state.Questions = state.Questions.Where(question => question.QuestionType == 1 && question.BloomLevel is >= 1 and <= 6
            && !string.IsNullOrWhiteSpace(question.OptionA) && !string.IsNullOrWhiteSpace(question.OptionB)
            && !string.IsNullOrWhiteSpace(question.OptionC) && !string.IsNullOrWhiteSpace(question.OptionD)).ToList();
        if (!state.ReadingTextId.HasValue || string.IsNullOrWhiteSpace(state.Content) || state.Questions.Count == 0)
            throw new BusinessRuleException("ExerciseSession.SkimmingContentUnavailable",
                "Bu seviyede ana fikir sorusu hazırlanmış Göz Gezdirme metni bulunamadı.");
        var content = ReadObject(config, "content");
        var rootContent = ReadObject(root, "content");
        var minimumWords = ReadPositiveInt(content, "minWordCount") ?? ReadPositiveInt(rootContent, "minWordCount") ?? 0;
        var maximumWords = ReadPositiveInt(content, "maxWordCount") ?? ReadPositiveInt(rootContent, "maxWordCount") ?? 0;
        var count = ReadPositiveInt(content, "questionCount") ?? ReadPositiveInt(rootContent, "questionCount");
        if (state.WordCount < minimumWords || maximumWords > 0 && state.WordCount > maximumWords
            || count.HasValue && state.Questions.Count < count.Value)
            throw new BusinessRuleException("ExerciseSession.SkimmingContentUnavailable",
                "Bu seviyede gerekli uzunlukta ve yeterli ana fikir sorusu bulunan metin hazırlanmadı.");
        if (count.HasValue) state.Questions = state.Questions.Take(count.Value).ToList();
        var timing = ReadObject(config, "timing");
        var rootTiming = ReadObject(root, "timing");
        state.ReadingMinimumMs = Math.Clamp(ReadNonNegativeInt(timing, "minReadingTimeMs")
            ?? ReadNonNegativeInt(rootTiming, "minReadingTimeMs") ?? 3000, 0, 3_600_000);
        state.ReadingMaximumMs = Math.Clamp(ReadPositiveInt(timing, "maxReadingTimeMs")
            ?? ReadPositiveInt(rootTiming, "maxReadingTimeMs")
            ?? Math.Clamp(ReadGridTimeLimit(config) ?? ReadGridTimeLimit(root) ?? 90, 1, 3600) * 1000, 1000, 3_600_000);
        if (state.ReadingMinimumMs >= state.ReadingMaximumMs)
            throw new BusinessRuleException("ExerciseSession.SkimmingTimingInvalid",
                "Minimum inceleme süresi, süre sınırından kısa olmalıdır.");
        state.ReadingPausedMilliseconds = 0;
        state.SkimmingProtocolVersion = 1;
        state.TotalSteps = state.Questions.Count + 1;
        state.TimeLimitSeconds = null;
    }
}
