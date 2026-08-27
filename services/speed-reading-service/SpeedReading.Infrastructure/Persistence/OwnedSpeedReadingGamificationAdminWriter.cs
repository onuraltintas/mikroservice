using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Gamification;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.Gamification;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingGamificationAdminWriter(OwnedSpeedReadingDbContext db)
    : ISpeedReadingGamificationAdminWriter
{
    private const string CreateScope = "speed-reading.achievements.create";
    private const string UpdateScope = "speed-reading.achievements.update";
    private const string DeleteScope = "speed-reading.achievements.delete";

    public async Task<AchievementAdminSummary> CreateAchievementAsync(
        Guid actorId,
        CreateAchievementRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        var key = idempotencyKey.Trim();
        var hash = RequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetAdminSummaryAsync(existing.ResourceId, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var achievement = Achievement.Create(
            Guid.NewGuid(),
            request.Name,
            request.Description,
            request.Category,
            request.Tier,
            request.IconUrl,
            request.IconEmoji,
            request.CriteriaType,
            request.CriteriaValue,
            request.TriggerType,
            request.TriggerValue,
            request.IsRepeatable,
            request.XpReward,
            request.IsActive,
            request.SortOrder,
            actorId,
            now);
        db.Achievements.Add(achievement);
        AddLedger(CreateScope, key, hash, achievement.Id, now);
        await SaveWithReplayAsync(CreateScope, key, hash, cancellationToken);
        return await GetAdminSummaryAsync(achievement.Id, cancellationToken);
    }

    public async Task<AchievementAdminSummary> UpdateAchievementAsync(
        Guid actorId,
        Guid achievementId,
        UpdateAchievementRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        var key = idempotencyKey.Trim();
        var hash = RequestHash(actorId, UpdateScope, achievementId, request);
        var existing = await GetLedgerAsync(UpdateScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return await GetAdminSummaryAsync(existing.ResourceId, cancellationToken);
        }

        var achievement = await db.Achievements.SingleOrDefaultAsync(
            item => item.Id == achievementId && !item.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Achievement", achievementId);
        achievement.Update(
            request.Name,
            request.Description,
            request.Category,
            request.Tier,
            request.IconUrl,
            request.IconEmoji,
            request.CriteriaType,
            request.CriteriaValue,
            request.TriggerType,
            request.TriggerValue,
            request.IsRepeatable,
            request.XpReward,
            request.IsActive,
            request.SortOrder,
            actorId,
            DateTime.UtcNow);
        AddLedger(UpdateScope, key, hash, achievement.Id, DateTime.UtcNow);
        await SaveWithReplayAsync(UpdateScope, key, hash, cancellationToken);
        return await GetAdminSummaryAsync(achievement.Id, cancellationToken);
    }

    public async Task DeleteAchievementAsync(
        Guid actorId,
        Guid achievementId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var hash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), DeleteScope, achievementId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplay(existing, hash);
            return;
        }

        var achievement = await db.Achievements.SingleOrDefaultAsync(
            item => item.Id == achievementId && !item.IsDeleted,
            cancellationToken) ?? throw new NotFoundException("Achievement", achievementId);
        if (await db.UserAchievements.AnyAsync(
                item => item.AchievementId == achievementId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "SpeedReading.Achievement.HasUnlocks",
                "Kazanımı kullanan öğrenci kayıtları varken silme yapılamaz; pasif hale getirin.");
        }

        var now = DateTime.UtcNow;
        achievement.Delete(actorId, now);
        AddLedger(DeleteScope, key, hash, achievement.Id, now);
        await SaveWithReplayAsync(DeleteScope, key, hash, cancellationToken);
    }

    private async Task<AchievementAdminSummary> GetAdminSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.Achievements.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == id && !value.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Achievement", id);
        var unlocked = await db.UserAchievements.CountAsync(
            value => value.AchievementId == id && !value.IsDeleted,
            cancellationToken);
        return new AchievementAdminSummary(
            item.Id, item.Name, item.Description, item.Category, item.Tier, item.IconUrl, item.IconEmoji,
            item.CriteriaType, item.CriteriaValue, item.TriggerType, item.TriggerValue, item.IsRepeatable,
            item.XPReward, item.IsActive, item.SortOrder, item.CreatedAt, item.UpdatedAt, unlocked);
    }

    private async Task<OwnedIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private void AddLedger(string scope, string key, string hash, Guid resourceId, DateTime createdAt) =>
        db.IdempotencyRecords.Add(new OwnedIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = hash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        });

    private async Task SaveWithReplayAsync(
        string scope,
        string key,
        string hash,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetLedgerAsync(scope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
            EnsureReplay(concurrent, hash);
        }
    }

    private static void ValidateRequest(Guid actorId, CreateAchievementRequest request, string key) =>
        ValidateRequest(actorId, key, request.Name, request.Description, request.Category, request.Tier,
            request.IconUrl, request.IconEmoji, request.CriteriaType, request.CriteriaValue,
            request.TriggerType, request.TriggerValue, request.XpReward, request.SortOrder);

    private static void ValidateRequest(Guid actorId, UpdateAchievementRequest request, string key) =>
        ValidateRequest(actorId, key, request.Name, request.Description, request.Category, request.Tier,
            request.IconUrl, request.IconEmoji, request.CriteriaType, request.CriteriaValue,
            request.TriggerType, request.TriggerValue, request.XpReward, request.SortOrder);

    private static void ValidateRequest(
        Guid actorId,
        string key,
        string name,
        string description,
        string category,
        string tier,
        string? iconUrl,
        string iconEmoji,
        string criteriaType,
        string criteriaValue,
        string? triggerType,
        int? triggerValue,
        int xpReward,
        int sortOrder)
    {
        ValidateIdempotency(actorId, key);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200
            || string.IsNullOrWhiteSpace(description) || description.Trim().Length > 500
            || string.IsNullOrWhiteSpace(category) || category.Trim().Length > 50
            || string.IsNullOrWhiteSpace(tier) || tier.Trim().Length > 50
            || iconUrl?.Trim().Length > 500 || iconEmoji.Trim().Length > 10
            || string.IsNullOrWhiteSpace(criteriaType) || criteriaType.Trim().Length > 100
            || string.IsNullOrWhiteSpace(criteriaValue) || criteriaValue.Length > 4_000
            || !IsJsonObjectOrArray(criteriaValue) || triggerType?.Trim().Length > 100
            || triggerValue is < 0 || xpReward is < 0 or > 1_000_000
            || sortOrder is < 0 or > 100_000)
        {
            throw new BusinessRuleException(
                "SpeedReading.Achievement.Invalid",
                "Kazanım alanları geçersiz.");
        }
    }

    private static bool IsJsonObjectOrArray(string value)
    {
        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string RequestHash(Guid actorId, string scope, Guid resourceId, object request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), JsonSerializer.Serialize(request));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static void EnsureReplay(OwnedIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
            throw new BusinessRuleException(
                "SpeedReading.IdempotencyKey.Reused",
                "Idempotency-Key farklı bir istek için tekrar kullanılamaz.");
    }

    private static void ValidateIdempotency(Guid actorId, string key)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(key)
            || key.Trim().Length is < 16 or > 128
            || key.Trim().Any(character => !(char.IsLetterOrDigit(character)
                || character is '.' or '_' or '-' or '~')))
        {
            throw new BusinessRuleException(
                "SpeedReading.IdempotencyKey.Invalid",
                "Geçerli bir Idempotency-Key gereklidir.");
        }
    }
}
