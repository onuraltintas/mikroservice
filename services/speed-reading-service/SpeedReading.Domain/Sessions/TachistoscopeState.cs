using System.Globalization;

namespace SpeedReading.Domain.Sessions;

// Product training defaults, not validated reading-speed or clinical norms.
public sealed class TachistoscopeState
{
    public string ContentType { get; set; } = "word";
    public string Source { get; set; } = "default_pool";
    public string[] Pool { get; set; } = [];
    /// <summary>Seed of the session's phrase pairing (null when the pool is not generated from pairs).</summary>
    public int? ContentSeed { get; set; }
    public int Round { get; set; }
    public int Count { get; set; } = 20;
    public int DisplayDurationMs { get; set; } = 500;
    public int InitialDurationMs { get; set; } = 500;
    public int MinDurationMs { get; set; } = 50;
    public int MaxDurationMs { get; set; } = 1000;
    public bool AdaptiveEnabled { get; set; } = true;
    public int TargetLength { get; set; } = 3;
    public int InitialTargetLength { get; set; } = 3;
    public int MinTargetLength { get; set; } = 1;
    public int MaxTargetLength { get; set; } = 12;
    public int ConsecutiveCorrect { get; set; }
    public string LastStimulus { get; set; } = string.Empty;
    public string ExpectedStimulus { get; set; } = string.Empty;
    public DateTime? PresentedAt { get; set; }
    public List<TachistoscopeTrial> Trials { get; set; } = [];
    public AssessmentBlockProtocol? AssessmentProtocol { get; set; }
    public List<string> AssessmentPresentedStimuli { get; set; } = [];

    public string SelectStimulus()
    {
        if (AssessmentProtocol is not null)
        {
            var used = Trials.Select(trial => trial.Stimulus).Concat(AssessmentPresentedStimuli).ToHashSet();
            var available = Enumerable.Range(100, 900).Select(value => value.ToString(CultureInfo.InvariantCulture))
                .Where(value => !used.Contains(value)).ToArray();
            var selected = available[Random.Shared.Next(available.Length)];
            AssessmentPresentedStimuli.Add(selected);
            return selected;
        }
        if (ContentType == "number" && Source != "custom")
            return string.Concat(Enumerable.Range(0, Math.Clamp(TargetLength, 1, 12)).Select(_ => Random.Shared.Next(10)));
        if (ContentType == "letter" && Source != "custom")
        {
            const string alphabet = "ABCÇDEFGĞHIİJKLMNOÖPRSŞTUÜVYZ";
            return string.Concat(Enumerable.Range(0, Math.Clamp(TargetLength, 1, 12)).Select(_ => alphabet[Random.Shared.Next(alphabet.Length)]));
        }
        var candidates = Pool.Where(word => word != LastStimulus).ToArray();
        if (candidates.Length == 0) candidates = Pool;
        if (candidates.Length == 0) throw new InvalidOperationException("Takistoskop için uygun içerik bulunamadı.");
        var distance = candidates.Min(word => Math.Abs(word.Length - TargetLength));
        candidates = candidates.Where(word => Math.Abs(word.Length - TargetLength) == distance).ToArray();
        var minimumUses = candidates.Min(word => Trials.Count(trial => Normalize(trial.Stimulus) == Normalize(word)));
        candidates = candidates.Where(word => Trials.Count(trial => Normalize(trial.Stimulus) == Normalize(word)) == minimumUses).ToArray();
        return candidates[Random.Shared.Next(candidates.Length)];
    }

    public bool Record(string stimulus, string answer, int responseTimeMs)
    {
        var correct = Normalize(stimulus) == Normalize(answer);
        Trials.Add(new(stimulus, answer, correct, Math.Max(0, responseTimeMs), DisplayDurationMs,
            AssessmentProtocol?.IsPractice(Round) == true));
        Round++;
        LastStimulus = stimulus;
        ConsecutiveCorrect = correct ? ConsecutiveCorrect + 1 : 0;
        if (AssessmentProtocol is not null) DisplayDurationMs = AssessmentProtocol.ValueAt(Round);
        else if (AdaptiveEnabled && !Trials[^1].IsPractice)
        {
            // Independent blocks of three scored trials; an incomplete final block changes nothing.
            var scored = Trials.Where(trial => !trial.IsPractice).ToList();
            if (scored.Count % AdaptationBlockSize == 0)
                Adapt(scored.TakeLast(AdaptationBlockSize).Count(trial => trial.IsCorrect));
        }
        return correct;
    }

    public const int AdaptationBlockSize = 3;

    // 3/3 correct: 15 % shorter; 2/3: unchanged; 0–1: 15 % longer. At a duration bound the length changes
    // instead and the duration returns to its start, so one block never makes both harder.
    private void Adapt(int correctCount)
    {
        if (correctCount == AdaptationBlockSize)
        {
            if (DisplayDurationMs <= MinDurationMs && TargetLength < MaxTargetLength)
            {
                TargetLength++;
                DisplayDurationMs = Math.Clamp(InitialDurationMs, MinDurationMs, MaxDurationMs);
            }
            else DisplayDurationMs = Math.Max(MinDurationMs, (int)Math.Round(DisplayDurationMs * .85, MidpointRounding.AwayFromZero));
        }
        else if (correctCount <= 1)
        {
            if (DisplayDurationMs >= MaxDurationMs && TargetLength > MinTargetLength)
            {
                TargetLength--;
                DisplayDurationMs = Math.Clamp(InitialDurationMs, MinDurationMs, MaxDurationMs);
            }
            else DisplayDurationMs = Math.Min(MaxDurationMs, (int)Math.Round(DisplayDurationMs * 1.15, MidpointRounding.AwayFromZero));
        }
        DisplayDurationMs = Math.Clamp(DisplayDurationMs, MinDurationMs, Math.Max(MinDurationMs, MaxDurationMs));
        TargetLength = Math.Clamp(TargetLength, Math.Min(MinTargetLength, MaxTargetLength), MaxTargetLength);
    }

    private static string Normalize(string value) => string.Join(' ', value.Trim().Split((char[]?)null,
        StringSplitOptions.RemoveEmptyEntries)).Normalize().ToLower(CultureInfo.GetCultureInfo("tr-TR"));
}

public sealed record TachistoscopeTrial(string Stimulus, string UserAnswer, bool IsCorrect, int ResponseTimeMs, int DisplayDurationMs, bool IsPractice = false);
