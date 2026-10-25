using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed record TargetCatalogPackage(string CatalogType, string Source, string SourceUrl,
    int? CatalogYear, int? PlacementYear, string? PlacementStage, IReadOnlyList<TargetAdmissionData> Rows);
public sealed record TargetCatalogImportResult(int Added, int Updated, int Total);

/// <summary>Atomically upserts reviewed source rows without deleting existing target relationships.</summary>
public sealed class TargetCatalogImporter(CoachingDbContext db)
{
    public async Task<TargetCatalogImportResult> ImportAsync(TargetCatalogPackage package, CancellationToken ct = default)
    {
        if (package.CatalogType is not ("School" or "UniversityProgram")
            || (package.CatalogType == "UniversityProgram" && package.Source != "mebi-yks")
            || string.IsNullOrWhiteSpace(package.Source) || package.Source.Length > 100
            || package.Rows.Count == 0 || package.Rows.Any(x => string.IsNullOrWhiteSpace(x.ExternalCode))
            || package.Rows.Select(x => x.ExternalCode).Distinct(StringComparer.Ordinal).Count() != package.Rows.Count)
            throw new ArgumentException("A reviewed catalog requires a type, source and unique source identifiers.");
        var prepared = package.Rows.Select(row =>
        {
            TargetCatalogEntry entry;
            if (package.CatalogType == "School")
            {
                if (row.ScoreType != "LGS") throw new ArgumentException("School rows must use LGS.");
                entry = TargetSchool.Create(package.Source, row.ExternalCode, row.Name,
                    row.City ?? "", row.District ?? "", row.MinimumScore, package.PlacementYear);
            }
            else
                entry = TargetUniversityProgram.Create(row.ExternalCode, row.Institution, row.Name,
                    row.Field, row.Degree, row.ScoreType);
            entry.UpdateDetails(row, package.CatalogYear, package.PlacementYear, package.PlacementStage, package.SourceUrl);
            entry.SetActive(true);
            return (row, entry);
        }).ToArray();

        async Task<TargetCatalogImportResult> RunAsync()
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '10s'", ct);
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE coaching.target_catalog IN SHARE ROW EXCLUSIVE MODE", ct);
            var schools = package.CatalogType == "School"
                ? await db.TargetSchools.Where(x => x.Source == package.Source).ToListAsync(ct) : [];
            var programs = package.CatalogType == "UniversityProgram"
                ? await db.TargetUniversityPrograms.ToListAsync(ct) : [];
            var added = 0; var updated = 0;
            foreach (var (row, candidate) in prepared)
            {
                TargetCatalogEntry? existing = package.CatalogType == "School"
                    ? schools.SingleOrDefault(x => x.SourceId == row.ExternalCode)
                    : programs.SingleOrDefault(x => x.ProgramCode == row.ExternalCode);
                if (existing is null) { db.Add(candidate); added++; continue; }
                if (existing is TargetUniversityProgram program)
                    program.UpdateOfficial(row.Institution, row.Name, row.ExternalCode, row.Field, row.Degree, row.ScoreType);
                else if (existing is TargetSchool school)
                {
                    if (school.City != row.City || school.District != row.District)
                        throw new ArgumentException("A source identity changed location; review the match before importing.");
                    school.Edit(row.Name, row.MinimumScore, package.PlacementYear);
                }
                existing.UpdateDetails(row, package.CatalogYear, package.PlacementYear, package.PlacementStage, package.SourceUrl);
                existing.SetActive(true); updated++;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(added, updated, prepared.Length);
        }
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(RunAsync);
    }
}
