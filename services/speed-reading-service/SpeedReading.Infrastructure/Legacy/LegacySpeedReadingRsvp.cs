using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Rsvp;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingRsvp(ISpeedReadingDataContext db) : ISpeedReadingRsvp
{
    public async Task<IReadOnlyList<RsvpSessionSummary>> GetSessionsAsync(
        Guid userId,
        int days,
        CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
        return await db.RsvpSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CreatedAt >= cutoff && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .Select(ToSummaryExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<RsvpSessionSummary?> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        return await db.RsvpSessions
            .AsNoTracking()
            .Where(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted)
            .Select(ToSummaryExpression)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<RsvpSessionSummary> CreateSessionAsync(
        Guid userId,
        CreateRsvpSessionRequest request,
        CancellationToken cancellationToken)
    {
        ValidateCreateRequest(request);
        var now = DateTime.UtcNow;
        var totalWords = request.TotalWords is > 0
            ? request.TotalWords.Value
            : CountWords(request.TextContent);
        if (totalWords <= 0)
        {
            throw new ArgumentException("TotalWords or TextContent is required.");
        }

        var completedWords = Math.Clamp(request.CompletedWords ?? 0, 0, totalWords);
        var completed = request.Completed ?? false;
        var session = new LegacyRsvpSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TextId = request.TextId,
            TextContent = NormalizeOptional(request.TextContent),
            WordsPerMinute = request.WordsPerMinute,
            FontFamily = NormalizeOptional(request.FontFamily) ?? "Arial",
            FontSize = request.FontSize is > 0 ? request.FontSize.Value : 24,
            BackgroundColor = NormalizeOptional(request.BackgroundColor) ?? "#ffffff",
            TextColor = NormalizeOptional(request.TextColor) ?? "#000000",
            TotalWords = totalWords,
            CompletedWords = completedWords,
            SessionDuration = request.SessionDuration.GetValueOrDefault(),
            Completed = completed,
            CompletedAt = completed ? now : null,
            CreatedAt = now,
            CreatedBy = userId
        };

        db.RsvpSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(session);
    }

    public async Task<bool> UpdateSessionAsync(
        Guid userId,
        Guid sessionId,
        UpdateRsvpSessionRequest request,
        CancellationToken cancellationToken)
    {
        var session = await db.RsvpSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (session is null)
        {
            return false;
        }

        if (request.CompletedWords.HasValue)
        {
            if (request.CompletedWords.Value < 0 || request.CompletedWords.Value > session.TotalWords)
            {
                throw new ArgumentException("CompletedWords must be between 0 and TotalWords.");
            }

            session.CompletedWords = request.CompletedWords.Value;
        }

        if (request.SessionDuration.HasValue)
        {
            if (request.SessionDuration.Value < 0)
            {
                throw new ArgumentException("SessionDuration cannot be negative.");
            }

            session.SessionDuration = request.SessionDuration.Value;
        }

        if (request.Completed.HasValue)
        {
            session.Completed = request.Completed.Value;
            session.CompletedAt = request.Completed.Value ? DateTime.UtcNow : null;
        }

        session.UpdatedAt = DateTime.UtcNow;
        session.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteSessionAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await db.RsvpSessions
            .SingleOrDefaultAsync(item => item.Id == sessionId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (session is null)
        {
            return false;
        }

        session.IsDeleted = true;
        session.DeletedAt = DateTime.UtcNow;
        session.DeletedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<RsvpStatistics> GetStatisticsAsync(Guid userId, int days, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 3650));
        var sessions = await db.RsvpSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.CreatedAt >= cutoff && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        return new RsvpStatistics(
            sessions.Count,
            sessions.Count(item => item.Completed),
            sessions.Count == 0 ? 0 : sessions.Average(item => item.WordsPerMinute),
            sessions.Sum(item => item.CompletedWords),
            sessions.Sum(item => item.SessionDuration) / 60);
    }

    private static void ValidateCreateRequest(CreateRsvpSessionRequest request)
    {
        if (request.WordsPerMinute is < 1 or > 2000)
        {
            throw new ArgumentException("WordsPerMinute must be between 1 and 2000.");
        }
        if (request.FontSize is < 8 or > 96)
        {
            throw new ArgumentException("FontSize must be between 8 and 96 when provided.");
        }
        if (request.SessionDuration is < 0)
        {
            throw new ArgumentException("SessionDuration cannot be negative.");
        }
        if (request.TotalWords is < 0 || request.CompletedWords is < 0)
        {
            throw new ArgumentException("Word counts cannot be negative.");
        }
        if (request.CompletedWords.HasValue && request.TotalWords.HasValue &&
            request.CompletedWords.Value > request.TotalWords.Value)
        {
            throw new ArgumentException("CompletedWords cannot exceed TotalWords.");
        }
    }

    private static int CountWords(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? 0
            : text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static RsvpSessionSummary ToSummary(LegacyRsvpSession session) =>
        new(
            session.Id,
            session.UserId,
            session.TextId,
            session.TextContent,
            session.WordsPerMinute,
            session.FontFamily,
            session.FontSize,
            session.BackgroundColor,
            session.TextColor,
            session.TotalWords,
            session.CompletedWords,
            session.SessionDuration,
            session.Completed,
            session.CreatedAt,
            session.CompletedAt);

    private static readonly System.Linq.Expressions.Expression<Func<LegacyRsvpSession, RsvpSessionSummary>> ToSummaryExpression =
        session => new RsvpSessionSummary(
            session.Id,
            session.UserId,
            session.TextId,
            session.TextContent,
            session.WordsPerMinute,
            session.FontFamily,
            session.FontSize,
            session.BackgroundColor,
            session.TextColor,
            session.TotalWords,
            session.CompletedWords,
            session.SessionDuration,
            session.Completed,
            session.CreatedAt,
            session.CompletedAt);
}
