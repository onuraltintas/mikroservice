using System.Text.Json;
using System.Text.RegularExpressions;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Infrastructure.Middleware;

namespace Coaching.Infrastructure.Catalogs;

// Administrative local import only. Not registered as an HTTP endpoint or startup seed.
public sealed class CoachingCatalogImporter(CoachingDbContext db)
{
    public sealed record Review(string Fingerprint, IReadOnlyDictionary<string, int> Counts, int NewRecords);
    public async Task<Review> ReviewAsync(IReadOnlyDictionary<string, string> files, string source, CancellationToken ct = default)
    {
        string fingerprint = "";
        var added = await ExecuteAsync(files, source, false, ct, true, capture: value => fingerprint = value);
        return new(fingerprint, CatalogPreflight.Validate(files).Counts, added);
    }

    public Task<int> ApproveAsync(IReadOnlyDictionary<string, string> files, string source, string fingerprint,
        string reason, Guid actorId, bool publish, CancellationToken ct = default)
    {
        if (fingerprint?.Length != 64 || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 200 || actorId == Guid.Empty)
            throw new ArgumentException("Approval requires a fingerprint, actor and reason.");
        return ExecuteAsync(files, source, publish, ct, expected: fingerprint, reason: reason.Trim(), actor: actorId, publish: publish);
    }
    public Task<int> PreviewAsync(IReadOnlyDictionary<string, string> files, string source,
        CancellationToken cancellationToken = default) => ExecuteAsync(files, source, false, cancellationToken, true);

    public Task<int> ImportAsync(IReadOnlyDictionary<string, string> files, string source,
        CancellationToken cancellationToken = default) => ExecuteAsync(files, source, false, cancellationToken);

    internal Task<int> VerifyAsync(IReadOnlyDictionary<string, string> files, string source,
        CancellationToken cancellationToken) => ExecuteAsync(files, source, true, cancellationToken);

    private async Task<int> ExecuteAsync(IReadOnlyDictionary<string, string> files, string source,
        bool verifyOnly, CancellationToken cancellationToken, bool previewOnly = false,
        Action<string>? capture = null, string? expected = null, string? reason = null, Guid? actor = null, bool publish = false)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Trim().Length > 100)
            throw new ArgumentException("A valid catalog source is required.", nameof(source));
        source = source.Trim();
        var report = CatalogPreflight.Validate(files);
        if (report.Errors.Count != 0) throw new ArgumentException("Catalog preflight failed: " + string.Join("; ", report.Errors.Take(10)));
        if (db.ChangeTracker.Entries().Any()) throw new InvalidOperationException("Import requires an unused dedicated context.");
        var rows = files.ToDictionary(x => x.Key, x => JsonSerializer.Deserialize<JsonElement[]>(x.Value)!);
        var auditId = Guid.NewGuid();
        async Task<int> RunAsync()
        {
            if (expected is not null)
            {
                var committed = await db.AdminAuditRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == auditId, cancellationToken);
                if (committed is not null) return JsonDocument.Parse(committed.ChangedFieldsJson!).RootElement.GetProperty("changed").GetInt32();
            }
            db.ChangeTracker.Clear();
            await using var transaction = db.Database.CurrentTransaction is null
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                // Serialize imports for this source. Unique indexes remain the final guard.
                await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", cancellationToken);
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({source}));", cancellationToken);
                // Share the deletion writer's table locks before reading its committed tombstones.
                await db.Database.ExecuteSqlRawAsync("""
                    LOCK TABLE coaching.study_catalog_lessons, coaching.study_catalog_topics,
                        coaching.study_catalog_units, coaching.target_schools,
                        coaching.target_university_programs IN SHARE ROW EXCLUSIVE MODE
                    """, cancellationToken);
                var deletions = await db.AdminAuditRecords.AsNoTracking()
                    .Where(x => x.ServiceName == "Coaching" && x.Action == "CatalogPermanentDelete")
                    .Select(x => new { x.ResourceType, x.ChangedFieldsJson }).ToListAsync(cancellationToken);
                var keys = new Dictionary<string, HashSet<string>>
                {
                    ["Lessons"] = rows["lessons.json"].Select(x => Text(x, "id")).ToHashSet(),
                    ["Units"] = rows["units-derived.json"].Select(x => Text(x, "id")).ToHashSet(),
                    ["Topics"] = rows["upper-subjects.json"].Select(x => "upper:" + Text(x, "id"))
                        .Concat(rows["subjects.json"].Select(x => "topic:" + Text(x, "id"))).ToHashSet(),
                    ["Schools"] = rows["lgs-programs.json"].Select(x => Text(x, "id")).ToHashSet(),
                    ["UniversityPrograms"] = rows["university-programs.json"].Select(x => Text(x, "id")).ToHashSet()
                };
                foreach (var deletion in deletions)
                {
                    using var payload = JsonDocument.Parse(deletion.ChangedFieldsJson ?? throw new InvalidOperationException("Deletion audit payload is missing."));
                    if (Text(payload.RootElement, "source") == source && deletion.ResourceType is not null
                        && keys.TryGetValue(deletion.ResourceType, out var candidates)
                        && candidates.Contains(Text(payload.RootElement, "sourceId")))
                        throw new InvalidOperationException("Reviewed files contain a permanently deleted catalog record.");
                }
                var lessons = await db.StudyCatalogLessons.Where(x => x.Source == source).ToDictionaryAsync(x => x.SourceId, cancellationToken);
                var units = await db.StudyCatalogUnits.Where(x => x.Source == source).ToDictionaryAsync(x => x.SourceId, cancellationToken);
                var topics = await db.StudyCatalogTopics.Where(x => x.Source == source).ToDictionaryAsync(x => x.SourceId, cancellationToken);
                var programs = await db.TargetUniversityPrograms.Where(x => x.Source == source).ToDictionaryAsync(x => x.SourceId, cancellationToken);
                var schools = await db.TargetSchools.Where(x => x.Source == source).ToDictionaryAsync(x => x.SourceId, cancellationToken);
                if (publish && (report.Counts.Values.Sum() == 0 || lessons.Count + units.Count + topics.Count + programs.Count + schools.Count != report.Counts.Values.Sum()))
                    throw new InvalidOperationException("Publication requires the complete reviewed source and no extra stored records.");
                var snapshot = JsonSerializer.Serialize(new { source,
                    files = files.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
                    lessons = lessons.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
                    units = units.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
                    topics = topics.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
                    programs = programs.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray(),
                    schools = schools.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray() });
                var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(snapshot)));
                capture?.Invoke(fingerprint);
                if (expected is not null && !string.Equals(expected, fingerprint, StringComparison.Ordinal))
                    throw new InvalidOperationException("Catalog or files changed after preview. Preview again.");
                var added = 0;
                foreach (var row in rows["lessons.json"])
                {
                    var label = Text(row, "gradeName");
                    var match = Regex.Match(label, @"^(1[0-2]|[1-9])\. Sınıf(?: \(2017-2023\))?$", RegexOptions.CultureInvariant);
                    int? grade = match.Success ? int.Parse(match.Groups[1].Value) : null;
                    var exam = label switch { "LGS" or "TYT" or "AYT" or "YDT" => label, "Temel Destek Paketleri" => "TDP", "Seçmeli" => null, _ when match.Success => null, _ => throw new ArgumentException("Unmapped grade label: " + label) };
                    added += Stage(lessons, Text(row, "id"), () => StudyCatalogLesson.Create(source, Text(row, "id"), Text(row, "name"), grade, exam));
                }
                foreach (var row in rows["units-derived.json"])
                    added += Stage(units, Text(row, "id"), () => StudyCatalogUnit.Create(source, Text(row, "id"), lessons[Text(row, "lessonId")].Id, Text(row, "name"), Number(row, "displayOrder")));
                foreach (var row in rows["upper-subjects.json"])
                    added += Stage(topics, "upper:" + Text(row, "id"), () => StudyCatalogTopic.Create(source, "upper:" + Text(row, "id"), lessons[Text(row, "lessonId")].Id, units[Text(row, "unitId")].Id, Text(row, "name"), null, Number(row, "rank")));
                foreach (var row in rows["subjects.json"])
                {
                    var parent = topics["upper:" + Text(row, "upperSubjectId")];
                    added += Stage(topics, "topic:" + Text(row, "id"), () => StudyCatalogTopic.Create(source, "topic:" + Text(row, "id"), parent.LessonId, parent.UnitId, Text(row, "name"), parent.Id, Number(row, "rank")));
                }
                foreach (var row in rows["university-programs.json"])
                    added += Stage(programs, Text(row, "id"), () => TargetUniversityProgram.Create(source, Text(row, "id"), Text(row, "university"), Text(row, "programName"), Text(row, "programCode"), Text(row, "scoreType"), Score(row), Number(row, "scoreYear")));
                foreach (var row in rows["lgs-programs.json"])
                    added += Stage(schools, Text(row, "id"), () => TargetSchool.Create(source, Text(row, "id"), Text(row, "schoolName"), Text(row, "city"), Text(row, "town"), Score(row), Number(row, "scoreYear")));
                if (verifyOnly && added != 0) throw new InvalidOperationException("Reviewed catalog contains records missing from storage.");
                if (publish)
                {
                    var entities = lessons.Values.Cast<object>().Concat(units.Values).Concat(topics.Values).Concat(programs.Values).Concat(schools.Values);
                    foreach (var entity in entities)
                    {
                        var active = (bool)entity.GetType().GetProperty("IsActive")!.GetValue(entity)!;
                        if (!active) { entity.GetType().GetMethod("SetActive")!.Invoke(entity, [true]); added++; }
                    }
                }
                if (expected is not null)
                    db.AdminAuditRecords.Add(new(auditId, DateTimeOffset.UtcNow, "Coaching", actor!.Value.ToString(), "SystemAdmin", null,
                        "POST", "/api/coaching-admin/catalog-imports", 200, Guid.NewGuid().ToString("N"), null, null,
                        publish ? "CatalogPublish" : "CatalogImport", "CatalogSource", source,
                        JsonSerializer.Serialize(new { reason, fingerprint, changed = added })));
                if ((!verifyOnly || publish) && !previewOnly) await db.SaveChangesAsync(cancellationToken);
                if (transaction is not null && !previewOnly) await transaction.CommitAsync(cancellationToken);
                return added;
            }
            finally { db.ChangeTracker.Clear(); }
        }
        if (db.Database.CurrentTransaction is not null)
        {
            if (!verifyOnly) throw new InvalidOperationException("Import requires its own transaction.");
            return await RunAsync();
        }
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(RunAsync);
    }

    private int Stage<T>(Dictionary<string, T> existing, string key, Func<T> create) where T : class
    {
        var candidate = create();
        if (existing.TryGetValue(key, out var current))
        {
            // Never overwrite active state or silently discard changed catalog content.
            if (typeof(T).GetProperties().Where(x => x.Name is not ("Id" or "IsActive"))
                .Any(x => !Equals(x.GetValue(current), x.GetValue(candidate))))
                throw new InvalidOperationException($"Catalog conflict: {typeof(T).Name}/{key}");
            return 0;
        }
        existing.Add(key, candidate);
        db.Set<T>().Add(candidate);
        return 1;
    }

    private static string Text(JsonElement row, string field) => row.TryGetProperty(field, out var value)
        && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;
    private static int? Number(JsonElement row, string field) => !row.TryGetProperty(field, out var value)
        || value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();
    private static decimal? Score(JsonElement row) => !row.TryGetProperty("minScore", out var value)
        || value.ValueKind == JsonValueKind.Null ? null : value.GetDecimal();
}
