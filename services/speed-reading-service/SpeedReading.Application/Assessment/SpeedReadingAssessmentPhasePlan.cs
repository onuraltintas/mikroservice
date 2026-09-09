using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.Assessment;

public enum AssessmentPhasePlanStatus
{
    Locked = 1,
    Available = 2,
    InProgress = 3,
    Completed = 4
}

public sealed record AssessmentPhasePlanAttemptInput(
    Guid AttemptId,
    AssessmentAttemptPhase Phase,
    AssessmentAttemptStatus Status,
    string FormVersion,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Language = "tr-TR");

public sealed record AssessmentPhasePlanItem(
    AssessmentAttemptPhase Phase,
    AssessmentPhasePlanStatus Status,
    AssessmentAttemptPhase? PrerequisitePhase,
    Guid? AttemptId,
    string FormVersion,
    DateTime? AvailableAt,
    DateTime? CompletedAt);

public sealed record AssessmentPhasePlanSummary(
    IReadOnlyList<AssessmentPhasePlanItem> Phases,
    AssessmentAttemptPhase? NextPhase);

public static class AssessmentPhaseTimingRules
{
    public static TimeSpan MinimumWait(AssessmentAttemptPhase phase) =>
        phase == AssessmentAttemptPhase.Retention
            ? TimeSpan.FromDays(7)
            : TimeSpan.Zero;

    public static DateTime? AvailableAt(
        AssessmentAttemptPhase phase,
        DateTime? prerequisiteCompletedAt)
    {
        if (!prerequisiteCompletedAt.HasValue)
            return null;

        var completedAt = prerequisiteCompletedAt.Value.Kind == DateTimeKind.Utc
            ? prerequisiteCompletedAt.Value
            : prerequisiteCompletedAt.Value.ToUniversalTime();
        return completedAt.Add(MinimumWait(phase));
    }
}

public static class AssessmentPhasePlanCalculator
{
    public static AssessmentPhasePlanSummary Calculate(
        IReadOnlyList<AssessmentPhasePlanAttemptInput> attempts,
        DateTime? nowUtc = null)
    {
        ArgumentNullException.ThrowIfNull(attempts);
        var now = EnsureUtc(nowUtc ?? DateTime.UtcNow);

        var latestCompletedByPhase = attempts
            .Where(item => item.Status == AssessmentAttemptStatus.Completed)
            .GroupBy(item => item.Phase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.CompletedAt ?? item.StartedAt)
                    .First());
        var latestInProgressByPhase = attempts
            .Where(item => item.Status == AssessmentAttemptStatus.InProgress)
            .GroupBy(item => item.Phase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.StartedAt).First());
        var language = attempts
            .OrderByDescending(item => item.StartedAt)
            .Select(item => item.Language)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
            ?? "tr-TR";

        var phases = Enum.GetValues<AssessmentAttemptPhase>()
            .Select(phase => BuildPhaseItem(
                phase,
                latestCompletedByPhase,
                latestInProgressByPhase,
                language,
                now))
            .ToList();
        var nextPhase = phases.FirstOrDefault(item => item.Status == AssessmentPhasePlanStatus.InProgress)?.Phase
            ?? phases.FirstOrDefault(item => item.Status == AssessmentPhasePlanStatus.Available)?.Phase;

        return new AssessmentPhasePlanSummary(phases, nextPhase);
    }

    private static AssessmentPhasePlanItem BuildPhaseItem(
        AssessmentAttemptPhase phase,
        IReadOnlyDictionary<AssessmentAttemptPhase, AssessmentPhasePlanAttemptInput> latestCompletedByPhase,
        IReadOnlyDictionary<AssessmentAttemptPhase, AssessmentPhasePlanAttemptInput> latestInProgressByPhase,
        string language,
        DateTime now)
    {
        AssessmentAttemptPhase? prerequisite = AssessmentAttemptPhaseRules.TryGetPrerequisite(
            phase,
            out var requiredPhase)
            ? requiredPhase
            : null;

        if (latestInProgressByPhase.TryGetValue(phase, out var inProgress))
        {
            return new AssessmentPhasePlanItem(
                phase,
                AssessmentPhasePlanStatus.InProgress,
                prerequisite,
                inProgress.AttemptId,
                inProgress.FormVersion,
                null,
                null);
        }

        if (latestCompletedByPhase.TryGetValue(phase, out var completed))
        {
            return new AssessmentPhasePlanItem(
                phase,
                AssessmentPhasePlanStatus.Completed,
                prerequisite,
                completed.AttemptId,
                completed.FormVersion,
                null,
                completed.CompletedAt);
        }

        AssessmentPhasePlanAttemptInput? prerequisiteAttempt = null;
        if (prerequisite.HasValue)
        {
            if (!latestCompletedByPhase.TryGetValue(prerequisite.Value, out var completedPrerequisite))
            {
                return new AssessmentPhasePlanItem(
                    phase,
                    AssessmentPhasePlanStatus.Locked,
                    prerequisite,
                    null,
                    GetDefaultFormVersion(phase, language),
                    null,
                    null);
            }

            prerequisiteAttempt = completedPrerequisite;
        }

        var prerequisiteCompletedAt = prerequisiteAttempt?.CompletedAt ?? prerequisiteAttempt?.StartedAt;
        var availableAt = AssessmentPhaseTimingRules.AvailableAt(phase, prerequisiteCompletedAt);
        var status = availableAt.HasValue && availableAt.Value > now
            ? AssessmentPhasePlanStatus.Locked
            : AssessmentPhasePlanStatus.Available;

        return new AssessmentPhasePlanItem(
            phase,
            status,
            prerequisite,
            null,
            GetDefaultFormVersion(phase, language),
            availableAt,
            null);
    }

    private static string GetDefaultFormVersion(
        AssessmentAttemptPhase phase,
        string language)
    {
        var languageCode = language
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(languageCode))
            languageCode = "tr";

        return $"{languageCode}-{phase.ToString().ToLowerInvariant()}-v1";
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
