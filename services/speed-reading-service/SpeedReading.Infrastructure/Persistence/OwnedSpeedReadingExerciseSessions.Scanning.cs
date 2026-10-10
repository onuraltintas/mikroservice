using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed partial class OwnedSpeedReadingExerciseSessions
{
    private static Guid[] ScanningTaskTextIds(JsonElement config)
    {
        var tasks = ReadProperty(ReadObject(config, "targets"), "tasksByTextId");
        if (tasks.ValueKind == JsonValueKind.Undefined) return [];
        if (tasks.ValueKind != JsonValueKind.Object)
            throw new BusinessRuleException("ExerciseSession.ScanningTasksUnavailable", "Bilgi bulma görevlerinin metin bağlantıları geçersiz.");
        var ids = new List<Guid>();
        foreach (var task in tasks.EnumerateObject())
        {
            if (!Guid.TryParseExact(task.Name, "D", out var id) || id == Guid.Empty || task.Name != id.ToString()
                || ids.Count >= 100 || ids.Contains(id))
                throw new BusinessRuleException("ExerciseSession.ScanningTasksUnavailable", "Bilgi bulma görevlerinin metin bağlantıları geçersiz.");
            ids.Add(id);
        }
        if (ids.Count == 0)
            throw new BusinessRuleException("ExerciseSession.ScanningTasksUnavailable", "Bilgi bulma görevleri henüz hazırlanmamış.");
        return ids.ToArray();
    }

    private static bool IsScanning(SessionState state) =>
        ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) is "scan_find" or "scanning";

    private static string NormalizeScanningWord(string word, bool caseSensitive)
    {
        var clean = Regex.Replace(word.Normalize(NormalizationForm.FormC), @"^[\p{P}\p{S}]+|[\p{P}\p{S}]+$", "");
        return caseSensitive ? clean : clean.ToLower(CultureInfo.GetCultureInfo("tr-TR"));
    }

    private static void InitializeScanning(SessionState state, JsonElement config)
    {
        var targetsConfig = ReadObject(config, "targets");
        state.ScanningCaseSensitive = ReadProperty(targetsConfig, "caseSensitive").ValueKind == JsonValueKind.True;
        state.ScanningFindAny = ReadString(targetsConfig, "mode") == "find_any";
        var configuredRounds = ReadProperty(config, "scanningRounds");
        if (configuredRounds.ValueKind == JsonValueKind.Array)
        {
            foreach (var round in configuredRounds.EnumerateArray().Take(50))
                state.ScanningRounds.Add(CreateScanningRound(
                    ReadString(round, "textContent") ?? "", ReadStringArray(round, "targets"), state, config));
        }
        else
        {
            var content = ReadString(ReadObject(config, "content"), "text") ?? state.Content;
            state.ScanningRounds.Add(CreateScanningRound(content, ReadStringArray(targetsConfig, "words"), state, config));
        }
        if (state.ScanningRounds.Count == 0)
            throw new BusinessRuleException("ExerciseSession.ScanningContentUnavailable", "Tarama metni ve hedefleri henüz hazırlanmadı.");
        state.Content = state.ScanningRounds[0].TextContent;
        state.ReadingPausedMilliseconds = 0;
        state.Questions.Clear();
        state.TotalSteps = state.ScanningRounds.Sum(round => state.ScanningFindAny ? 1 : round.Targets.Length);
        state.TimeLimitSeconds = Math.Clamp(ReadGridTimeLimit(config) ?? 90, 1, 3600);
    }

    private static ScanningRound CreateScanningRound(string content, string[] targets, SessionState state, JsonElement config)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 100_000)
            throw new BusinessRuleException("ExerciseSession.ScanningContentUnavailable", "Tarama için uygun bir metin bulunamadı.");
        var words = SplitWords(content).Select(word => NormalizeScanningWord(word, state.ScanningCaseSensitive))
            .Where(word => word.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var requested = targets.Take(100).Select(word => NormalizeScanningWord(word, state.ScanningCaseSensitive))
            .Where(word => word.Length > 0).Distinct(StringComparer.Ordinal).ToArray();
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var taskMap = ReadObject(ReadObject(config, "targets"), "tasksByTextId");
        if (taskMap.ValueKind == JsonValueKind.Object)
        {
            var tasks = ReadProperty(taskMap, state.ReadingTextId?.ToString() ?? "");
            if (tasks.ValueKind != JsonValueKind.Array || tasks.GetArrayLength() == 0)
                throw new BusinessRuleException("ExerciseSession.ScanningTasksUnavailable", "Seçilen metnin bilgi bulma görevleri hazırlanmamış.");
            foreach (var task in tasks.EnumerateArray())
            {
                var answer = NormalizeScanningWord(ReadString(task, "answer") ?? "", state.ScanningCaseSensitive);
                var prompt = ReadString(task, "prompt") ?? "";
                if (answer.Length == 0 || string.IsNullOrWhiteSpace(prompt) || !words.Contains(answer, StringComparer.Ordinal)
                    || !labels.TryAdd(answer, prompt))
                    throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Bilgi bulma görevi metindeki tek bir sözcükle eşleşmelidir.");
            }
            requested = labels.Keys.ToArray();
        }
        if (targets.Length > 0 && (requested.Length == 0 || requested.Any(word => !words.Contains(word, StringComparer.Ordinal))))
            throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Tarama hedefleri metindeki sözcüklerle eşleşmiyor.");
        var requiredCount = Math.Clamp(ReadPositiveInt(config, "targetCount") ?? 3, 1, 100);
        if (targets.Length == 0 && labels.Count == 0)
        {
            var targetConfig = ReadObject(config, "targets");
            var minimumLength = ReadPositiveInt(targetConfig, "minLength") ?? 2;
            var maximumLength = ReadPositiveInt(targetConfig, "maxLength") ?? 100;
            var candidates = words.Where(word => word.Length >= minimumLength && word.Length <= maximumLength && word.Any(char.IsLetter)).ToArray();
            Random.Shared.Shuffle(candidates);
            requested = candidates.Take(requiredCount).ToArray();
            if (requested.Length < requiredCount)
                throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Metinde istenen sayıda farklı tarama hedefi bulunamadı.");
        }
        else if (labels.Count > 0 && requested.Length != requiredCount)
            throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Bilgi bulma görev sayısı hedef sayısıyla eşleşmiyor.");
        if (requested.Length == 0)
            throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Metinde taranabilecek hedef sözcük bulunamadı.");
        var contentConfig = ReadObject(config, "content");
        var count = SplitWords(content).Length;
        if (count < (ReadPositiveInt(contentConfig, "minWordCount") ?? 0)
            || count > (ReadPositiveInt(contentConfig, "maxWordCount") ?? 100_000))
            throw new BusinessRuleException("ExerciseSession.ScanningContentUnavailable", "Tarama metni yapılandırılan uzunluk sınırlarıyla eşleşmiyor.");
        return new ScanningRound { TextContent = content, Targets = requested, TargetLabels = labels };
    }

    private static bool ScanningComplete(SessionState state) =>
        state.ScanningRounds.Count > 0 && state.CurrentRound >= state.ScanningRounds.Count;

    private static decimal ScanningAccuracy(SessionState state) => state.TotalSteps > 0
        ? Math.Round(100m * state.ScanningRounds.Sum(round => round.FoundTargets.Count) / state.TotalSteps, 2) : 0;

    private static bool ScanningExpired(ExerciseSession session, SessionState state, DateTime now) =>
        !ScanningComplete(state) && state.TimingStartedAt.HasValue && state.TimeLimitSeconds.HasValue
        && ScanningActiveMs(state, now) >= state.TimeLimitSeconds.Value * 1000L;

    private static long ScanningActiveMs(SessionState state, DateTime now) => state.TimingStartedAt.HasValue
        ? (long)Math.Max(0, ((state.ReadingPausedAt ?? now) - state.TimingStartedAt.Value).TotalMilliseconds
            - (state.ReadingPausedMilliseconds ?? 0)) : 0;

    private static void UpdateScanningTime(ExerciseSession session, SessionState state, DateTime now)
    {
        state.ScanningElapsedMs = Math.Min(ScanningActiveMs(state, now), (state.TimeLimitSeconds ?? 3600) * 1000L);
        if (state.CurrentRound < state.ScanningRounds.Count)
            state.ScanningRounds[state.CurrentRound].SearchTimeMs = Math.Max(0, state.ScanningElapsedMs.Value
                - state.ScanningRounds.Take(state.CurrentRound).Sum(round => round.SearchTimeMs));
    }

    private static ExerciseActionValidationResponse ValidateScanning(ExerciseSession session, SessionState state,
        ExerciseActionRequest request, DateTime now)
    {
        if (state.ScanningRounds.Count == 0) return Invalid("Tarama oturumunu yeniden başlatın.");
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("scan_start" or "scan_click" or "scan_timeout"))
            return Invalid("Tarama yalnız doğrulanmış hedef tıklamalarıyla ilerler.");
        if (ScanningComplete(state) || state.ReadingIncomplete)
            return action == "scan_start" ? ScanningResponse(session, state, now) : Invalid("Tarama zaten tamamlandı.");
        if (action == "scan_start")
        {
            EnsureTimingStarted(session, state, now);
            UpdateScanningTime(session, state, now);
            return ScanningResponse(session, state, now);
        }
        if (!state.TimingStartedAt.HasValue) return Invalid("Önce taramayı başlatın.");
        if (ScanningExpired(session, state, now))
        {
            UpdateScanningTime(session, state, now);
            state.ReadingIncomplete = true;
            return ScanningResponse(session, state, now);
        }
        if (action == "scan_timeout") return Invalid("Tarama süresi henüz dolmadı.");
        if (request.Number != state.CurrentRound) return Invalid("Tarama turu güncel değil.");
        var round = state.ScanningRounds[state.CurrentRound];
        var tokens = SplitWords(round.TextContent);
        if (request.Index is not { } index || index < 0 || index >= tokens.Length) return Invalid("Geçersiz sözcük konumu.");
        var word = NormalizeScanningWord(tokens[index], state.ScanningCaseSensitive);
        if (round.FoundTargets.Contains(word, StringComparer.Ordinal)) return Invalid("Bu hedef zaten bulundu.");
        var correct = round.Targets.Contains(word, StringComparer.Ordinal);
        UpdateScanningTime(session, state, now);
        if (correct)
        {
            round.TargetResponseTimesMs.Add(Math.Max(0, round.SearchTimeMs - round.LastTargetElapsedMs));
            round.LastTargetElapsedMs = round.SearchTimeMs;
            round.FoundTargets.Add(word);
            session.Advance();
            round.IsCompleted = state.ScanningFindAny || round.FoundTargets.Count == round.Targets.Length;
            if (round.IsCompleted) state.CurrentRound++;
        }
        else session.RecordIncorrectAttempt();
        return ScanningResponse(session, state, now, correct);
    }

    private static ExerciseActionValidationResponse ScanningResponse(ExerciseSession session, SessionState state, DateTime now, bool? correct = null) =>
        Valid("Tarama durumu doğrulandı.", nextStep: session.CurrentStep,
            isCompleted: ScanningComplete(state) || state.ReadingIncomplete, isCorrect: correct,
            feedbackData: JsonSerializer.SerializeToElement(new {
                state.ScanningRounds, state.CurrentRound, state.ScanningCaseSensitive, state.ScanningFindAny,
                state.TotalSteps, session.CorrectCount, session.IncorrectCount,
                timedOut = state.ReadingIncomplete,
                searchTimeMs = state.ScanningElapsedMs ?? 0
            }, JsonOptions));

    private sealed class ScanningRound
    {
        public Dictionary<string, string> TargetLabels { get; set; } = [];
        public string TextContent { get; set; } = "";
        public string[] Targets { get; set; } = [];
        public List<string> FoundTargets { get; set; } = [];
        public long SearchTimeMs { get; set; }
        public bool IsCompleted { get; set; }
        public List<long> TargetResponseTimesMs { get; set; } = [];
        public long LastTargetElapsedMs { get; set; }
    }
}
