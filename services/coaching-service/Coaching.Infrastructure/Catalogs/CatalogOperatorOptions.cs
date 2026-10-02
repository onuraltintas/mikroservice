namespace Coaching.Infrastructure.Catalogs;

public sealed record CatalogOperatorOptions(string Directory, string Source, string Action,
    bool Apply, string? Database, bool PublicationAuthorized)
{
    public static CatalogOperatorOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (key is "--apply" or "--publication-authorized")
            {
                if (!flags.Add(key)) throw new ArgumentException("Duplicate option.");
            }
            else if (key is "--directory" or "--source" or "--action" or "--database")
            {
                if (++index == args.Length || args[index].StartsWith("--", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(args[index]) || !values.TryAdd(key, args[index]))
                    throw new ArgumentException("Missing or duplicate option value.");
            }
            else throw new ArgumentException("Unknown option.");
        }
        if (!values.TryGetValue("--directory", out var directory) || !values.TryGetValue("--source", out var source)
            || source.Trim().Length > 100) throw new ArgumentException("Directory and valid source are required.");
        var action = values.GetValueOrDefault("--action", "check");
        if (action is not ("check" or "import" or "publish" or "unpublish")) throw new ArgumentException("Unknown action.");
        var apply = flags.Contains("--apply");
        var database = values.GetValueOrDefault("--database");
        var authorized = flags.Contains("--publication-authorized");
        if (apply && (action == "check" || database is null || action == "publish" && !authorized))
            throw new ArgumentException("Writes require an action, confirmed database, and publication rights when publishing.");
        return new(directory, source.Trim(), action, apply, database, authorized);
    }
}
