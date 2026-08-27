using System.Globalization;
using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Idempotent program-template commands backed only by the owned store.
/// </summary>
internal sealed class OwnedSpeedReadingProgramAdminWriter(OwnedSpeedReadingDbContext db)
    : ISpeedReadingProgramAdminWriter
{
    private const string IdempotencyKeyPattern = "^[A-Za-z0-9._~-]{16,128}$";
    private const string CreateScope = "speed-reading.program-templates.create";
    private const string UpdateScope = "speed-reading.program-templates.update";
    private const string DeleteScope = "speed-reading.program-templates.delete";
    private const string CloneScope = "speed-reading.program-templates.clone";

    public async Task<ExerciseProgramTemplateAdminSummary> CreateExerciseProgramTemplateAsync(
        Guid actorId,
        CreateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, key, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, requestHash, cancellationToken);

        var now = DateTime.UtcNow;
        var template = ProgramTemplate.Create(
            request.Name,
            request.Description,
            request.TargetAgeGroupConfigurationId,
            request.MinAssessmentScore,
            request.MaxAssessmentScore,
            request.WeeklyPatternJson,
            request.InitialDifficultyLevel,
            request.WeeksPerDifficultyIncrease,
            request.MaxDifficultyLevel,
            request.TotalWeeks,
            request.TotalDays,
            request.IsActive,
            request.DisplayOrder,
            request.ProgramType,
            request.ExamType,
            request.IsAssessment,
            actorId,
            now);
        db.ProgramTemplates.Add(template);
        db.IdempotencyRecords.Add(CreateLedger(CreateScope, key, requestHash, template.Id, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetLedgerAsync(CreateScope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
            return await ReplayAsync(concurrent, requestHash, cancellationToken);
        }

        return ToSummary(template);
    }

    public async Task<ExerciseProgramTemplateAdminSummary> UpdateExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        UpdateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = CreateRequestHash(actorId, UpdateScope, programTemplateId, request);
        var existing = await GetLedgerAsync(UpdateScope, key, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, requestHash, cancellationToken);

        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == programTemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProgramTemplate", programTemplateId);
        var now = DateTime.UtcNow;
        template.Update(
            request.Name,
            request.Description,
            request.TargetAgeGroupConfigurationId,
            request.MinAssessmentScore,
            request.MaxAssessmentScore,
            request.WeeklyPatternJson,
            request.InitialDifficultyLevel,
            request.WeeksPerDifficultyIncrease,
            request.MaxDifficultyLevel,
            request.TotalWeeks,
            request.TotalDays,
            request.IsActive,
            request.DisplayOrder,
            request.ProgramType,
            request.ExamType,
            request.IsAssessment,
            actorId,
            now);
        db.IdempotencyRecords.Add(CreateLedger(UpdateScope, key, requestHash, template.Id, now));

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetLedgerAsync(UpdateScope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
            return await ReplayAsync(concurrent, requestHash, cancellationToken);
        }

        return ToSummary(template);
    }

    public async Task DeleteExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            DeleteScope,
            programTemplateId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == programTemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProgramTemplate", programTemplateId);
        if (await db.StudentProgramProgresses.AnyAsync(
                item => item.ProgramTemplateId == programTemplateId,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "ProgramTemplate.HasProgress",
                "Program şablonu, bağlı öğrenci ilerlemesi kaldırılmadan silinemez.");
        }

        var now = DateTime.UtcNow;
        template.Delete(actorId, now);
        db.IdempotencyRecords.Add(CreateLedger(DeleteScope, key, requestHash, template.Id, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetLedgerAsync(DeleteScope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
            EnsureReplayMatches(concurrent, requestHash);
        }
    }

    public async Task<ExerciseProgramTemplateAdminSummary> CloneExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            CloneScope,
            programTemplateId.ToString("D"));
        var existing = await GetLedgerAsync(CloneScope, key, cancellationToken);
        if (existing is not null)
            return await ReplayAsync(existing, requestHash, cancellationToken);

        var source = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == programTemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ProgramTemplate", programTemplateId);
        var now = DateTime.UtcNow;
        var clone = source.Clone(Guid.NewGuid(), actorId, now);
        db.ProgramTemplates.Add(clone);
        db.IdempotencyRecords.Add(CreateLedger(CloneScope, key, requestHash, clone.Id, now));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetLedgerAsync(CloneScope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
            return await ReplayAsync(concurrent, requestHash, cancellationToken);
        }

        return ToSummary(clone);
    }

    private async Task<OwnedIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private async Task<ExerciseProgramTemplateAdminSummary> ReplayAsync(
        OwnedIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var template = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait program şablonu bulunamadı; yeni bir anahtar kullanın.");
        return ToSummary(template);
    }

    private static OwnedIdempotencyRecord CreateLedger(
        string scope,
        string key,
        string requestHash,
        Guid resourceId,
        DateTime createdAt) => new()
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        };

    private static void ValidateIdempotency(Guid actorId, string idempotencyKey)
    {
        if (actorId == Guid.Empty
            || !Regex.IsMatch(idempotencyKey?.Trim() ?? string.Empty, IdempotencyKeyPattern))
        {
            throw new ArgumentException(
                "Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.",
                nameof(idempotencyKey));
        }
    }

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateExerciseProgramTemplateRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            scope,
            resourceId.ToString("D"),
            request.Name,
            request.Description,
            request.TargetAgeGroupConfigurationId.ToString("D"),
            request.MinAssessmentScore.ToString(CultureInfo.InvariantCulture),
            request.MaxAssessmentScore.ToString(CultureInfo.InvariantCulture),
            request.WeeklyPatternJson,
            request.InitialDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.WeeksPerDifficultyIncrease.ToString(CultureInfo.InvariantCulture),
            request.MaxDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.TotalWeeks.ToString(CultureInfo.InvariantCulture),
            request.TotalDays.ToString(CultureInfo.InvariantCulture),
            request.IsActive.ToString(),
            request.DisplayOrder.ToString(CultureInfo.InvariantCulture),
            request.ProgramType.ToString(CultureInfo.InvariantCulture),
            request.ExamType,
            request.IsAssessment.ToString());

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateExerciseProgramTemplateRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"),
            scope,
            resourceId.ToString("D"),
            request.Name,
            request.Description,
            request.TargetAgeGroupConfigurationId.ToString("D"),
            request.MinAssessmentScore.ToString(CultureInfo.InvariantCulture),
            request.MaxAssessmentScore.ToString(CultureInfo.InvariantCulture),
            request.WeeklyPatternJson,
            request.InitialDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.WeeksPerDifficultyIncrease.ToString(CultureInfo.InvariantCulture),
            request.MaxDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.TotalWeeks.ToString(CultureInfo.InvariantCulture),
            request.TotalDays.ToString(CultureInfo.InvariantCulture),
            request.IsActive.ToString(),
            request.DisplayOrder.ToString(CultureInfo.InvariantCulture),
            request.ProgramType.ToString(CultureInfo.InvariantCulture),
            request.ExamType,
            request.IsAssessment.ToString());

    private static void EnsureReplayMatches(OwnedIdempotencyRecord record, string requestHash)
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
            "ix_idempotency_records_scope_key",
            StringComparison.Ordinal);

    private static ExerciseProgramTemplateAdminSummary ToSummary(ProgramTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.TargetAgeGroupConfigurationId,
        template.MinAssessmentScore,
        template.MaxAssessmentScore,
        template.WeeklyPatternJson,
        template.InitialDifficultyLevel,
        template.WeeksPerDifficultyIncrease,
        template.MaxDifficultyLevel,
        template.TotalWeeks,
        template.TotalDays,
        template.IsActive,
        template.DisplayOrder,
        template.ProgramType,
        template.ExamType,
        template.IsAssessment);
}
