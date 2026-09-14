namespace SpeedReading.Application.AdaptiveLearning;

public enum AdaptiveProgressionDecisionKind
{
    CollectEvidence = 0,
    Advance = 1,
    Maintain = 2,
    Support = 3
}

public sealed record AdaptiveProgressionPolicy(
    int MinimumMeasuredSessions,
    decimal AdvanceComprehensionThreshold,
    decimal MaintainComprehensionThreshold,
    decimal MinimumWpmTrendPercent,
    decimal SupportTrendPercent)
{
    public static AdaptiveProgressionPolicy Default { get; } = new(
        MinimumMeasuredSessions: 3,
        AdvanceComprehensionThreshold: 80,
        MaintainComprehensionThreshold: 65,
        MinimumWpmTrendPercent: 0,
        SupportTrendPercent: -10);
}

public sealed record AdaptiveProgressionEvidence(decimal? Wpm, decimal Comprehension);

public sealed record AdaptiveProgressionDecision(
    AdaptiveProgressionDecisionKind Kind,
    int DifficultyAdjustment,
    string Reason);

public static class AdaptiveProgressionRules
{
    public static AdaptiveProgressionDecision Evaluate(
        IReadOnlyList<AdaptiveProgressionEvidence> measuredSessions,
        AdaptiveProgressionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(measuredSessions);
        ArgumentNullException.ThrowIfNull(policy);
        Validate(policy);

        var evidence = measuredSessions
            .Where(item => item.Comprehension is >= 0 and <= 100)
            .ToArray();
        if (evidence.Length < policy.MinimumMeasuredSessions)
        {
            return new AdaptiveProgressionDecision(
                AdaptiveProgressionDecisionKind.CollectEvidence,
                0,
                $"Karar için en az {policy.MinimumMeasuredSessions} ölçülmüş oturum gerekir.");
        }

        var recent = evidence.TakeLast(policy.MinimumMeasuredSessions).ToArray();
        var latestComprehension = recent[^1].Comprehension;
        var averageComprehension = recent.Average(item => item.Comprehension);
        var wpmTrendPercent = CalculateWpmTrendPercent(recent);

        if (latestComprehension < policy.MaintainComprehensionThreshold
            || averageComprehension < policy.MaintainComprehensionThreshold
            || wpmTrendPercent is not null && wpmTrendPercent <= policy.SupportTrendPercent)
        {
            return new AdaptiveProgressionDecision(
                AdaptiveProgressionDecisionKind.Support,
                -1,
                "Son ölçümlerde anlama veya okuma hızı geriledi; destek paketi atanmalı.");
        }

        if (latestComprehension >= policy.AdvanceComprehensionThreshold
            && averageComprehension >= policy.AdvanceComprehensionThreshold
            && (wpmTrendPercent is null || wpmTrendPercent >= policy.MinimumWpmTrendPercent))
        {
            return new AdaptiveProgressionDecision(
                AdaptiveProgressionDecisionKind.Advance,
                1,
                "Son ölçümlerde anlama korundu ve okuma hızı gerilemedi; bir sonraki seviye açılabilir.");
        }

        return new AdaptiveProgressionDecision(
            AdaptiveProgressionDecisionKind.Maintain,
            0,
            "Öğrenci mevcut seviyede farklı içeriklerle pekiştirmeye devam etmeli.");
    }

    private static decimal? CalculateWpmTrendPercent(IReadOnlyList<AdaptiveProgressionEvidence> evidence)
    {
        var first = evidence.FirstOrDefault(item => item.Wpm is > 0)?.Wpm;
        var latest = evidence.LastOrDefault(item => item.Wpm is > 0)?.Wpm;
        if (first is null || latest is null || first <= 0)
            return null;

        return Math.Round(((latest.Value - first.Value) / first.Value) * 100m, 2);
    }

    private static void Validate(AdaptiveProgressionPolicy policy)
    {
        if (policy.MinimumMeasuredSessions is < 2 or > 10
            || policy.AdvanceComprehensionThreshold is < 0 or > 100
            || policy.MaintainComprehensionThreshold is < 0 or > 100
            || policy.MaintainComprehensionThreshold > policy.AdvanceComprehensionThreshold
            || policy.MinimumWpmTrendPercent is < -100 or > 100
            || policy.SupportTrendPercent is < -100 or > 0)
        {
            throw new ArgumentOutOfRangeException(nameof(policy), "Adaptation policy thresholds are invalid.");
        }
    }
}
