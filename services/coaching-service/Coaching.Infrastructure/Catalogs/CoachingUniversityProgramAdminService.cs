using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed record UniversityProgramRow(Guid Id, string UniversityName, string Name, string? ProgramCode, string? FacultyName, string? ProgramType,
    string? ScoreType, string? City, int? Quota, int? Placed, decimal? MinimumScore, decimal? MaximumScore, int? Rank, int? Year, int? CatalogYear,
    bool IsActive, string Source, string? SourceUrl, DateTimeOffset? UpdatedAt);
public sealed record UniversityProgramDocument(string Fingerprint, UniversityProgramRow Data, int GoalCount);
public sealed record UniversityProgramSaveRequest(string Reason, string? Fingerprint = null, string? Name = null, string? FacultyName = null,
    decimal? MinimumScore = null, decimal? MaximumScore = null, int? Rank = null, int? Quota = null, int? Placed = null, int? Year = null, bool? IsActive = null,
    // Create only:
    string? UniversityName = null, string? ProgramCode = null, string? ProgramType = null, string? ScoreType = null, string? City = null);
public sealed record UniversityProgramStatusRequest(string Fingerprint, bool IsActive, string Reason);
public sealed record UniversityProgramDeleteRequest(string Fingerprint, string Reason);

/// <summary>
/// KM7 (01.11.2026): the global administrator manages the university program catalog (target_catalog, CatalogType
/// UniversityProgram): search, correct the score/rank/quota data, switch programs on and off, add a manual program and delete
/// a program no goal uses. Every write takes a 5–500 character reason, the current fingerprint and writes an audit row.
/// </summary>
public sealed class CoachingUniversityProgramAdminService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope, ICurrentUserService user)
{
    public async Task<TargetSearchPage<UniversityProgramRow>> ListAsync(string? search, string? scoreType, string? city, int? year, bool? active,
        int page, int pageSize, CancellationToken ct)
    {
        await Require(ct);
        if (page is < 1 or > 100000 || pageSize is < 1 or > 200 || search?.Length > 200 || city?.Length > 100 || year is < 1900 or > 2100)
            throw new ArgumentException("Filtreleri ve sayfayı kontrol edin.");
        var type = string.IsNullOrWhiteSpace(scoreType) ? null : NormalizeType(scoreType);
        var query = db.TargetUniversityPrograms.AsNoTracking().Where(x => (type == null || x.ScoreType == type) && (year == null || x.ScoreYear == year)
            && (active == null || x.IsActive == active));
        if (!string.IsNullOrWhiteSpace(city)) query = query.WhereContains(city, x => x.City);
        query = query.WhereContains(search, x => x.UniversityName, x => x.Name, x => x.ProgramCode);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.UniversityName).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return new(rows.Select(Row).ToArray(), total, page, pageSize);
    }

    public async Task<UniversityProgramDocument> GetAsync(Guid id, CancellationToken ct)
    { await Require(ct); return await Document(await Load(id, ct, tracked: false), ct); }

    public async Task<UniversityProgramDocument> CreateAsync(UniversityProgramSaveRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r.Reason);
        if (r.Fingerprint is not null) throw new ArgumentException("Yeni kayıtta parmak izi kullanılmaz.");
        var program = TargetUniversityProgram.CreateManual(r.UniversityName ?? "", r.Name ?? "", r.ProgramCode, r.FacultyName, r.ProgramType,
            NormalizeType(r.ScoreType) ?? throw new ArgumentException("Puan türü seçin."), r.City);
        program.EditAdmin(program.Name, program.FacultyName, r.MinimumScore, r.MaximumScore, r.Rank, r.Quota, r.Placed, r.Year);
        program.SetActive(r.IsActive ?? false);
        return await Write("UniversityProgramCreate", r.Reason, null, async () =>
        {
            if (program.ProgramCode is not null && program.IsActive && await db.TargetUniversityPrograms.AnyAsync(x => x.ProgramCode == program.ProgramCode && x.IsActive, ct))
                throw new BusinessRuleException("Catalog.InUse", "Bu program kodu etkin başka bir programda kullanılıyor.");
            db.Add(program); return program;
        }, ct);
    }

    public async Task<UniversityProgramDocument> UpdateAsync(Guid id, UniversityProgramSaveRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r.Reason);
        return await Write("UniversityProgramUpdate", r.Reason, (id, r.Fingerprint), async () =>
        {
            var program = await Load(id, ct);
            program.EditAdmin(r.Name ?? program.Name, r.FacultyName, r.MinimumScore, r.MaximumScore, r.Rank, r.Quota, r.Placed, r.Year);
            if (r.IsActive is { } active) await SetActive(program, active, ct);
            return program;
        }, ct);
    }

    public async Task<UniversityProgramDocument> SetActiveAsync(Guid id, UniversityProgramStatusRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r.Reason);
        return await Write("UniversityProgramStatus", r.Reason, (id, r.Fingerprint), async () =>
        {
            var program = await Load(id, ct); await SetActive(program, r.IsActive, ct); return program;
        }, ct);
    }

    public async Task DeleteAsync(Guid id, UniversityProgramDeleteRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r.Reason);
        await Write("UniversityProgramDelete", r.Reason, (id, r.Fingerprint), async () =>
        {
            var program = await Load(id, ct);
            var goals = await db.AcademicGoals.CountAsync(x => x.TargetUniversityProgramId == id, ct);
            if (goals > 0) throw new BusinessRuleException("Catalog.InUse", $"Bu program {goals} öğrenci hedefinde kullanılıyor; silinemez. Pasife alabilirsiniz.");
            db.Remove(program); return program;
        }, ct);
    }

    private async Task SetActive(TargetUniversityProgram program, bool active, CancellationToken ct)
    {
        if (active && !program.IsActive && program.ProgramCode is not null
            && await db.TargetUniversityPrograms.AnyAsync(x => x.ProgramCode == program.ProgramCode && x.IsActive && x.Id != program.Id, ct))
            throw new BusinessRuleException("Catalog.InUse", "Aynı program kodu etkin başka bir programda var; önce onu pasife alın.");
        program.SetActive(active);
    }

    private async Task<UniversityProgramDocument> Write(string action, string reason, (Guid Id, string? Fingerprint)? current,
        Func<Task<TargetUniversityProgram>> change, CancellationToken ct)
    {
        UniversityProgramDocument? result = null;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (db.Database.IsRelational())
                {
                    await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
                    await db.Database.ExecuteSqlRawAsync("LOCK TABLE coaching.target_catalog, coaching.academic_goals IN SHARE ROW EXCLUSIVE MODE", ct);
                }
                string? before = null;
                if (current is { } c)
                {
                    before = (await Document(await Load(c.Id, ct, tracked: false), ct)).Fingerprint;
                    if (string.IsNullOrEmpty(c.Fingerprint) || before != c.Fingerprint)
                        throw new BusinessRuleException("Catalog.Stale", "Program başka bir işlemde değişmiş. Güncel kaydı açıp yeniden deneyin.");
                }
                var program = await change();
                var deleted = db.Entry(program).State == EntityState.Deleted;
                await db.SaveChangesAsync(ct);
                var after = deleted ? null : (await Document(program, ct)).Fingerprint;
                var label = $"{program.UniversityName} · {program.Name}";
                db.AdminAuditRecords.Add(new AdminAuditRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, "Coaching", user.UserId!.Value.ToString(), "SystemAdmin", null,
                    "COMMAND", "/api/coaching-admin/catalog/university-programs", 200, Guid.NewGuid().ToString(), null, null, action, "UniversityProgram", program.Id.ToString(),
                    JsonSerializer.Serialize(new { reason = reason.Trim(), before, after, name = label[..Math.Min(160, label.Length)],
                        program.ProgramCode, program.Source }, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));
                await db.SaveChangesAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
                result = deleted ? null : await Document(program, ct);
                db.ChangeTracker.Clear();
            }
            catch { db.ChangeTracker.Clear(); throw; }
        });
        return result!;
    }

    private async Task<TargetUniversityProgram> Load(Guid id, CancellationToken ct, bool tracked = true)
    {
        var query = tracked ? db.TargetUniversityPrograms : db.TargetUniversityPrograms.AsNoTracking();
        return await query.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new KeyNotFoundException("Üniversite programı bulunamadı.");
    }

    private async Task<UniversityProgramDocument> Document(TargetUniversityProgram p, CancellationToken ct)
    {
        var row = Row(p);
        var canonical = JsonSerializer.Serialize(row with { UpdatedAt = null, MinimumScore = Canon(row.MinimumScore), MaximumScore = Canon(row.MaximumScore) });
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))), row,
            await db.AcademicGoals.CountAsync(x => x.TargetUniversityProgramId == p.Id, ct));
    }

    // numeric(12,5) pads the scale; equal scores must give equal fingerprints.
    private static decimal? Canon(decimal? value) => value is null ? null : decimal.Parse(value.Value.ToString("G29", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static UniversityProgramRow Row(TargetUniversityProgram x) => new(x.Id, x.UniversityName, x.Name, x.ProgramCode, x.FacultyName, x.ProgramType,
        x.ScoreType, x.City, x.Quota, x.Placed, x.MinimumScore, x.MaximumScore, x.Rank, x.ScoreYear, x.CatalogYear, x.IsActive, x.Source, x.SourceUrl, x.UpdatedAt);

    /// <summary>Score types as the catalog stores them (TYT, SAY, EA, SOZ, YDT); SÖZ, DİL and DIL are accepted.</summary>
    private static string? NormalizeType(string? value) => value?.Trim().ToUpperInvariant().Replace('Ö', 'O').Replace('İ', 'I') switch
    {
        null or "" => null, "TYT" => "TYT", "SAY" => "SAY", "EA" => "EA", "SOZ" => "SOZ", "DIL" or "YDT" => "YDT",
        _ => throw new ArgumentException("Puan türü TYT, SAY, EA, SÖZ ya da DİL olmalıdır.")
    };

    private static void Reason(string? reason)
    { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("5–500 karakterlik işlem gerekçesi girin."); }

    private async Task Require(CancellationToken ct)
    { if (!user.IsAuthenticated || !user.UserId.HasValue || !(await scope.RequireReadScopeAsync(ct)).IsGlobal) throw new BusinessRuleException("Authorization.Forbidden", "Üniversite programlarını yalnız global yönetici yönetebilir."); }
}
