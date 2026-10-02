using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

// Operator-only; deliberately not registered as a public endpoint or startup action.
public sealed class CoachingCatalogPublication(CoachingDbContext db)
{
    public async Task<int> SetPublishedAsync(string source, IReadOnlyDictionary<string, string> files, bool published,
        bool publicationAuthorized, CancellationToken cancellationToken = default)
    {
        if (published && !publicationAuthorized) throw new UnauthorizedAccessException("Catalog publication rights must be confirmed.");
        var expected = CatalogPreflight.Validate(files);
        if (string.IsNullOrWhiteSpace(source) || source.Trim().Length > 100 || expected.Errors.Count != 0)
            throw new ArgumentException("A valid source and successful preflight are required.");
        source = source.Trim();
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Publication requires an unused dedicated context.");
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({source}));", cancellationToken);
            // Verify actual content under the same lock/transaction as activation, not just row counts.
            await new CoachingCatalogImporter(db).VerifyAsync(files, source, cancellationToken);
            var counts = new Dictionary<string, int>
            {
                ["lessons.json"] = await db.StudyCatalogLessons.CountAsync(x => x.Source == source, cancellationToken),
                ["units-derived.json"] = await db.StudyCatalogUnits.CountAsync(x => x.Source == source, cancellationToken),
                ["upper-subjects.json"] = await db.StudyCatalogTopics.CountAsync(x => x.Source == source && x.ParentId == null, cancellationToken),
                ["subjects.json"] = await db.StudyCatalogTopics.CountAsync(x => x.Source == source && x.ParentId != null, cancellationToken),
                ["university-programs.json"] = await db.TargetUniversityPrograms.CountAsync(x => x.Source == source, cancellationToken),
                ["lgs-programs.json"] = await db.TargetSchools.CountAsync(x => x.Source == source, cancellationToken)
            };
            if (expected.Counts.Count != counts.Count || counts.Any(x => !expected.Counts.TryGetValue(x.Key, out var value) || value != x.Value)
                || counts.Values.Sum() == 0)
                throw new InvalidOperationException("Stored catalog counts do not match the verified source files.");
            var changed = await db.StudyCatalogLessons.Where(x => x.Source == source && x.IsActive != published)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsActive, published), cancellationToken);
            changed += await db.StudyCatalogUnits.Where(x => x.Source == source && x.IsActive != published)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsActive, published), cancellationToken);
            changed += await db.StudyCatalogTopics.Where(x => x.Source == source && x.IsActive != published)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsActive, published), cancellationToken);
            changed += await db.TargetUniversityPrograms.Where(x => x.Source == source && x.IsActive != published)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsActive, published), cancellationToken);
            changed += await db.TargetSchools.Where(x => x.Source == source && x.IsActive != published)
                .ExecuteUpdateAsync(x => x.SetProperty(row => row.IsActive, published), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return changed;
        });
    }
}
