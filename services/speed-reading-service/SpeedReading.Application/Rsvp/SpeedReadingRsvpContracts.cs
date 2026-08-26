namespace SpeedReading.Application.Rsvp;

public interface ISpeedReadingRsvp
{
    Task<IReadOnlyList<RsvpSessionSummary>> GetSessionsAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken);

    Task<RsvpSessionSummary?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken);

    Task<RsvpSessionSummary> CreateSessionAsync(
        Guid userId,
        CreateRsvpSessionRequest request,
        CancellationToken cancellationToken);

    Task<bool> UpdateSessionAsync(
        Guid userId,
        Guid sessionId,
        UpdateRsvpSessionRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken);

    Task<RsvpStatistics> GetStatisticsAsync(Guid userId, int days, CancellationToken cancellationToken);
}

public sealed record RsvpSessionSummary(
    Guid Id,
    Guid UserId,
    Guid? TextId,
    string? TextContent,
    int WordsPerMinute,
    string FontFamily,
    int FontSize,
    string BackgroundColor,
    string TextColor,
    int TotalWords,
    int CompletedWords,
    int SessionDuration,
    bool Completed,
    DateTime CreatedAt,
    DateTime? CompletedAt);

public sealed record CreateRsvpSessionRequest(
    Guid? TextId,
    string? TextContent,
    int WordsPerMinute,
    string? FontFamily,
    int? FontSize,
    string? BackgroundColor,
    string? TextColor,
    int? TotalWords = null,
    int? CompletedWords = null,
    int? SessionDuration = null,
    bool? Completed = null);

public sealed record UpdateRsvpSessionRequest(
    int? CompletedWords,
    int? SessionDuration,
    bool? Completed);

public sealed record RsvpStatistics(
    int TotalSessions,
    int CompletedSessions,
    double AverageWpm,
    int TotalWords,
    int TotalMinutes);
