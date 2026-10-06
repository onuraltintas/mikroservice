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
    private static bool IsScanning(SessionState state) =>
        ExerciseConfigurationRules.NormalizeEngineType(state.EngineType) is "scan_find" or "scanning" or "skimming";

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
        if (targets.Length > 0 && (requested.Length == 0 || requested.Any(word => !words.Contains(word, StringComparer.Ordinal))))
            throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Tarama hedefleri metindeki sözcüklerle eşleşmiyor.");
        if (targets.Length == 0)
            requested = words.Where(word => word.Any(char.IsLetter)).OrderByDescending(word => word.Length)
                .ThenBy(word => word, StringComparer.Ordinal).Take(Math.Clamp(ReadPositiveInt(config, "targetCount") ?? 3, 1, 100)).ToArray();
        if (requested.Length == 0)
            throw new BusinessRuleException("ExerciseSession.ScanningTargetsUnavailable", "Metinde taranabilecek hedef sözcük bulunamadı.");
        return new ScanningRound { TextContent = content, Targets = requested };
    }

    private static bool ScanningComplete(SessionState state) =>
        state.ScanningRounds.Count > 0 && state.CurrentRound >= state.ScanningRounds.Count;

    private static decimal ScanningAccuracy(SessionState state) => state.TotalSteps > 0
        ? Math.Round(100m * state.ScanningRounds.Sum(round => round.FoundTargets.Count) / state.TotalSteps, 2) : 0;

    private static bool ScanningExpired(ExerciseSession session, SessionState state, DateTime now) =>
        !ScanningComplete(state) && state.TimingStartedAt.HasValue && state.TimeLimitSeconds.HasValue
        && (now - state.TimingStartedAt.Value).TotalSeconds - GetTimingPausedSeconds(session, state) >= state.TimeLimitSeconds.Value;

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
            return ScanningResponse(session, state, now);
        }
        if (!state.TimingStartedAt.HasValue) return Invalid("Önce taramayı başlatın.");
        if (ScanningExpired(session, state, now))
        {
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
        if (correct)
        {
            round.FoundTargets.Add(word);
            session.Advance();
            round.IsCompleted = state.ScanningFindAny || round.FoundTargets.Count == round.Targets.Length;
            if (round.IsCompleted) state.CurrentRound++;
        }
        else session.RecordIncorrectAttempt();
        round.SearchTimeMs = (long)Math.Max(0, (now - state.TimingStartedAt.Value).TotalMilliseconds - GetTimingPausedSeconds(session, state) * 1000L);
        return ScanningResponse(session, state, now, correct);
    }

    private static ExerciseActionValidationResponse ScanningResponse(ExerciseSession session, SessionState state, DateTime now, bool? correct = null) =>
        Valid("Tarama durumu doğrulandı.", nextStep: session.CurrentStep,
            isCompleted: ScanningComplete(state) || state.ReadingIncomplete, isCorrect: correct,
            feedbackData: JsonSerializer.SerializeToElement(new {
                state.ScanningRounds, state.CurrentRound, state.ScanningCaseSensitive, state.ScanningFindAny,
                state.TotalSteps, session.CorrectCount, session.IncorrectCount,
                timedOut = state.ReadingIncomplete,
                searchTimeMs = state.TimingStartedAt.HasValue ? Math.Max(0,
                    (now - state.TimingStartedAt.Value).TotalMilliseconds - GetTimingPausedSeconds(session, state) * 1000L) : 0
            }, JsonOptions));

    private sealed class ScanningRound
    {
        public string TextContent { get; set; } = "";
        public string[] Targets { get; set; } = [];
        public List<string> FoundTargets { get; set; } = [];
        public long SearchTimeMs { get; set; }
        public bool IsCompleted { get; set; }
    }
}
