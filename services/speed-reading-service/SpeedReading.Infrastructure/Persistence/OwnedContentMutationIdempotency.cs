using System.Text.Json;
using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Progress;

namespace SpeedReading.Infrastructure.Persistence;

internal static class OwnedContentMutationIdempotency
{
    private const string IdempotencyKeyPattern = "^[A-Za-z0-9._~-]{16,128}$";

    public static void Validate(Guid actorId, string idempotencyKey)
    {
        if (actorId == Guid.Empty || !Regex.IsMatch(idempotencyKey?.Trim() ?? string.Empty, IdempotencyKeyPattern))
            throw new ArgumentException("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.", nameof(idempotencyKey));
    }

    public static string CreateRequestHash(Guid actorId, string scope, Guid resourceId, object? request = null) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            scope,
            resourceId.ToString("D"),
            request is null ? string.Empty : JsonSerializer.Serialize(request));

    public static Task<OwnedIdempotencyRecord?> GetAsync(
        OwnedSpeedReadingDbContext db,
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    public static void Add(
        OwnedSpeedReadingDbContext db,
        string scope,
        string key,
        string requestHash,
        Guid resourceId,
        DateTime createdAt) =>
        db.IdempotencyRecords.Add(new OwnedIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        });

    public static void EnsureReplayMatches(OwnedIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
            throw new BusinessRuleException("Idempotency.Conflict", "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
    }

    public static async Task<OwnedIdempotencyRecord?> SaveAsync(
        OwnedSpeedReadingDbContext db,
        string scope,
        string key,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (DbUpdateException exception) when (IsConflict(exception))
        {
            db.ChangeTracker.Clear();
            return await GetAsync(db, scope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
        }
    }

    private static bool IsConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == "23505"
        && string.Equals(postgres.ConstraintName, "ix_idempotency_records_scope_key", StringComparison.Ordinal);
}
