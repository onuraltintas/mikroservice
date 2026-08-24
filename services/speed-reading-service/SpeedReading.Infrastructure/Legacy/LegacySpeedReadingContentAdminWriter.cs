using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingContentAdminWriter(SpeedReadingDbContext db)
    : ISpeedReadingContentAdminWriter
{
    private const string CreateScope = "speed-reading.exercise-types.create";
    private const string UpdateScope = "speed-reading.exercise-types.update";
    private const string DeleteScope = "speed-reading.exercise-types.delete";
    private const string IdempotencyKeyPattern = "^[A-Za-z0-9._~-]{16,128}$";

    public async Task<ExerciseTypeSummary> CreateExerciseTypeAsync(
        Guid actorId,
        CreateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, requestHash, cancellationToken);
        }

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var now = DateTime.UtcNow;
        var exerciseType = new LegacyExerciseType
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IconName = request.IconName?.Trim() ?? string.Empty,
            ColorCode = request.ColorCode?.Trim() ?? string.Empty,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            EngineType = request.EngineType.Trim(),
            CategoryId = request.CategoryId,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.ExerciseTypes.Add(exerciseType);
        AddLedger(CreateScope, idempotencyKey, requestHash, exerciseType.Id, now);
        await SaveOrReplayAsync(exerciseType.Id, CreateScope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(exerciseType);
    }

    public async Task<ExerciseTypeSummary> UpdateExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        UpdateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = CreateRequestHash(actorId, UpdateScope, exerciseTypeId, request);
        var existing = await GetLedgerAsync(UpdateScope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, requestHash, cancellationToken);
        }

        var exerciseType = await db.ExerciseTypes
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseType", exerciseTypeId);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        exerciseType.Name = request.Name.Trim();
        exerciseType.DisplayName = request.DisplayName.Trim();
        exerciseType.Description = request.Description?.Trim() ?? string.Empty;
        exerciseType.IconName = request.IconName?.Trim() ?? string.Empty;
        exerciseType.ColorCode = request.ColorCode?.Trim() ?? string.Empty;
        exerciseType.SortOrder = request.SortOrder;
        exerciseType.IsActive = request.IsActive;
        exerciseType.EngineType = request.EngineType.Trim();
        exerciseType.CategoryId = request.CategoryId;
        exerciseType.UpdatedAt = DateTime.UtcNow;
        exerciseType.UpdatedBy = actorId;

        AddLedger(UpdateScope, idempotencyKey, requestHash, exerciseType.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(exerciseType.Id, UpdateScope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(exerciseType);
    }

    public async Task DeleteExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), DeleteScope, exerciseTypeId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var exerciseType = await db.ExerciseTypes
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseType", exerciseTypeId);
        var now = DateTime.UtcNow;
        exerciseType.IsDeleted = true;
        exerciseType.DeletedAt = now;
        exerciseType.DeletedBy = actorId;
        exerciseType.UpdatedAt = now;
        exerciseType.UpdatedBy = actorId;
        AddLedger(DeleteScope, idempotencyKey, requestHash, exerciseType.Id, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == DeleteScope && item.Key == idempotencyKey, cancellationToken);
            EnsureReplayMatches(concurrent, requestHash);
        }
    }

    private async Task<ExerciseTypeSummary> ReplayAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var exerciseType = await db.ExerciseTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait egzersiz türü bulunamadı; yeni bir anahtar kullanın.");
        return ToSummary(exerciseType);
    }

    private async Task SaveOrReplayAsync(
        Guid resourceId,
        string scope,
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == scope && item.Key == key, cancellationToken);
            EnsureReplayMatches(concurrent, requestHash);
            if (concurrent.ResourceId != resourceId)
            {
                throw new BusinessRuleException(
                    "Idempotency.Conflict",
                    "Aynı Idempotency-Key farklı bir kaynağa karşı kullanılamaz.");
            }
        }
    }

    private async Task<LegacyIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private void AddLedger(string scope, string key, string requestHash, Guid resourceId, DateTime createdAt) =>
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        });

    private async Task EnsureCategoryExistsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await db.ExerciseTypeCategories
            .AsNoTracking()
            .AnyAsync(item => item.Id == categoryId && !item.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("ExerciseTypeCategory", categoryId.Value);
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateExerciseTypeRequest request,
        string idempotencyKey) =>
        ValidateRequest(actorId, idempotencyKey, request.Name, request.DisplayName,
            request.Description, request.IconName, request.ColorCode, request.SortOrder, request.EngineType);

    private static void ValidateRequest(
        Guid actorId,
        UpdateExerciseTypeRequest request,
        string idempotencyKey) =>
        ValidateRequest(actorId, idempotencyKey, request.Name, request.DisplayName,
            request.Description, request.IconName, request.ColorCode, request.SortOrder, request.EngineType);

    private static void ValidateRequest(
        Guid actorId,
        string idempotencyKey,
        string name,
        string displayName,
        string? description,
        string? iconName,
        string? colorCode,
        int sortOrder,
        string engineType)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            || string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 150
            || (description?.Length ?? 0) > 1_000
            || (iconName?.Length ?? 0) > 100
            || string.IsNullOrWhiteSpace(engineType) || engineType.Trim().Length > 100
            || sortOrder is < 0 or > 10_000)
        {
            throw new ArgumentException("Egzersiz türü alanları geçersiz.", nameof(name));
        }

        if (!string.IsNullOrWhiteSpace(colorCode)
            && !Regex.IsMatch(colorCode.Trim(), "^#[0-9a-fA-F]{6}$"))
        {
            throw new ArgumentException("ColorCode #RRGGBB biçiminde olmalıdır.", nameof(colorCode));
        }
    }

    private static void ValidateIdempotency(Guid actorId, string idempotencyKey)
    {
        if (actorId == Guid.Empty || !Regex.IsMatch(idempotencyKey?.Trim() ?? string.Empty, IdempotencyKeyPattern))
        {
            throw new ArgumentException(
                "Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.",
                nameof(idempotencyKey));
        }
    }

    private static string NormalizeKey(string key) => key.Trim();

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateExerciseTypeRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.DisplayName, request.Description, request.IconName, request.ColorCode,
            request.SortOrder.ToString(), request.IsActive.ToString(), request.EngineType,
            request.CategoryId?.ToString("D"));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateExerciseTypeRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.DisplayName, request.Description, request.IconName, request.ColorCode,
            request.SortOrder.ToString(), request.IsActive.ToString(), request.EngineType,
            request.CategoryId?.ToString("D"));

    private static void EnsureReplayMatches(LegacyIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
        {
            throw new BusinessRuleException(
                "Idempotency.Conflict",
                "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
        }
    }

    private static bool IsIdempotencyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == "23505"
        && string.Equals(
            postgresException.ConstraintName,
            "UX_SpeedReadingIdempotencyRecords_Scope_Key",
            StringComparison.Ordinal);

    private static ExerciseTypeSummary ToSummary(LegacyExerciseType type) => new(
        type.Id,
        type.Name,
        type.DisplayName,
        type.Description,
        type.IconName,
        type.ColorCode,
        type.SortOrder,
        type.IsActive,
        type.EngineType,
        type.CategoryId);
}
