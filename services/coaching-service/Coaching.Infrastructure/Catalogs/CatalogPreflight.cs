using System.Text.Json;

namespace Coaching.Infrastructure.Catalogs;

public sealed record CatalogPreflightReport(IReadOnlyDictionary<string, int> Counts, IReadOnlyList<string> Errors);

// Deliberately has no database dependency: validation cannot import or activate records.
public static class CatalogPreflight
{
    private static readonly string[] FileNames = ["lessons.json", "units-derived.json", "upper-subjects.json",
        "subjects.json", "university-programs.json", "lgs-programs.json"];

    public static async Task<CatalogPreflightReport> ReadAsync(string directory, CancellationToken cancellationToken = default)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var name in FileNames)
        {
            var path = Path.Combine(Path.GetFullPath(directory), name);
            var info = new FileInfo(path);
            if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Catalog symbolic links are not allowed.");
            if (info.Length > 32 * 1024 * 1024)
                throw new ArgumentException("Catalog file exceeds the 32 MiB limit.");
            files.Add(name, await File.ReadAllTextAsync(path, cancellationToken));
        }
        return Validate(files);
    }

    public static CatalogPreflightReport Validate(IReadOnlyDictionary<string, string> files)
    {
        if (files.Count != FileNames.Length || files.Keys.Any(x => !FileNames.Contains(x, StringComparer.Ordinal)))
            throw new ArgumentException("Only the six approved catalog files are accepted.");
        var counts = new Dictionary<string, int>();
        var errors = new List<string>();
        var rows = new Dictionary<string, Dictionary<string, JsonElement>>();
        foreach (var name in FileNames)
        {
            using var doc = JsonDocument.Parse(files[name]);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException($"{name}: catalog must be an array.");
            counts[name] = doc.RootElement.GetArrayLength();
            var indexed = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var row in doc.RootElement.EnumerateArray())
            {
                var id = Text(row, "id");
                if (string.IsNullOrWhiteSpace(id)) { errors.Add($"{name}: missing id"); continue; }
                if (!indexed.TryAdd(id, row.Clone())) errors.Add($"{name}: duplicate id {id}");
                var nameField = name == "university-programs.json" ? "programName" : name == "lgs-programs.json" ? "schoolName" : "name";
                if (string.IsNullOrWhiteSpace(Text(row, nameField))) errors.Add($"{name}: {id} missing {nameField}");
            }
            rows[name] = indexed;
        }
        foreach (var (id, unit) in rows["units-derived.json"])
            if (!rows["lessons.json"].ContainsKey(Text(unit, "lessonId"))) errors.Add($"units-derived.json: {id} invalid lessonId");
        foreach (var (id, parent) in rows["upper-subjects.json"])
        {
            if (!rows["units-derived.json"].TryGetValue(Text(parent, "unitId"), out var unit))
                errors.Add($"upper-subjects.json: {id} invalid unitId");
            else if (Text(unit, "lessonId") != Text(parent, "lessonId"))
                errors.Add($"upper-subjects.json: {id} mismatched lessonId");
        }
        foreach (var (id, topic) in rows["subjects.json"])
        {
            if (!rows["upper-subjects.json"].TryGetValue(Text(topic, "upperSubjectId"), out var parent))
                errors.Add($"subjects.json: {id} invalid upperSubjectId");
            else if (Text(topic, "unitId") != Text(parent, "unitId"))
                errors.Add($"subjects.json: {id} mismatched parent unit");
        }
        return new CatalogPreflightReport(counts, errors);
    }

    private static string Text(JsonElement row, string field) => row.ValueKind == JsonValueKind.Object
        && row.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString() ?? string.Empty : string.Empty;
}
