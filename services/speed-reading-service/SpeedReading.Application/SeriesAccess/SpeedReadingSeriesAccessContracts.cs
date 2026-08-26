namespace SpeedReading.Application.SeriesAccess;

public sealed record SeriesAccessSummary(
    Guid SeriesId,
    string SeriesName,
    string SeriesTitle,
    string Description,
    int SequenceOrder,
    bool IsUnlocked,
    bool CanUnlock,
    string? LockReason,
    Guid? PrerequisiteSeriesId,
    string? PrerequisiteSeriesTitle,
    string? PrerequisiteSeriesName,
    int? PrerequisiteCompletionPercentage,
    int RequiredCompletionPercentage,
    int TotalDays,
    int EstimatedDurationDays,
    string MinimumLevel,
    string MaximumLevel);

public sealed record SeriesPrerequisiteSummary(
    bool IsMet,
    string Message,
    Guid? RequiredSeriesId,
    string? RequiredSeriesName,
    int RequiredCompletionPercentage,
    int CurrentCompletionPercentage);

public sealed record UnlockSeriesResult(
    bool Success,
    string Message,
    Guid? UnlockedSeriesId);

public interface ISpeedReadingSeriesAccess
{
    Task<IReadOnlyList<SeriesAccessSummary>> GetAvailableAsync(Guid userId, CancellationToken cancellationToken);
    Task<SeriesAccessSummary?> CheckAccessAsync(Guid userId, Guid seriesId, CancellationToken cancellationToken);
    Task<SeriesPrerequisiteSummary?> CheckPrerequisitesAsync(Guid userId, Guid seriesId, CancellationToken cancellationToken);
    Task<UnlockSeriesResult?> UnlockAsync(Guid userId, Guid seriesId, CancellationToken cancellationToken);
}
