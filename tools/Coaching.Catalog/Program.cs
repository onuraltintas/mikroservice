using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

try
{
    var options = CatalogOperatorOptions.Parse(args);
    var files = await CatalogPreflight.ReadFilesAsync(options.Directory);
    var report = CatalogPreflight.Validate(files);
    foreach (var count in report.Counts) Console.WriteLine($"{count.Key}: {count.Value}");
    if (report.Errors.Count != 0) throw new ArgumentException("Catalog validation failed.");
    if (!options.Apply)
    {
        Console.WriteLine("Read-only preflight complete. No database was connected or changed.");
        return 0;
    }
    // Credentials stay in the operator's environment, never in arguments or output.
    var connection = Environment.GetEnvironmentVariable("COACHING_CATALOG_CONNECTION");
    if (string.IsNullOrWhiteSpace(connection)) throw new ArgumentException("COACHING_CATALOG_CONNECTION is required.");
    var parsed = new NpgsqlConnectionStringBuilder(connection);
    if (!string.Equals(parsed.Database, options.Database, StringComparison.Ordinal))
        throw new ArgumentException("The connection database does not match the explicit confirmation.");
    await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
        .UseNpgsql(connection, x => { x.EnableRetryOnFailure(); x.MigrationsHistoryTable("__ef_migrations_history", "coaching"); }).Options);
    // This tool never creates/migrates a schema, edits users or deletes catalog history.
    if (!await db.Database.CanConnectAsync() || (await db.Database.GetPendingMigrationsAsync()).Any()
        || db.Database.HasPendingModelChanges())
        throw new InvalidOperationException("A reachable, fully migrated Coaching database is required.");
    var changed = options.Action == "import"
        ? await new CoachingCatalogImporter(db).ImportAsync(files, options.Source)
        : await new CoachingCatalogPublication(db).SetPublishedAsync(options.Source, files,
            options.Action == "publish", options.PublicationAuthorized);
    Console.WriteLine($"{options.Action}: {changed} catalog rows changed.");
    return 0;
}
catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException
    or InvalidOperationException or NpgsqlException)
{
    // Database exceptions may contain credentials/personal data: do not print their details.
    Console.Error.WriteLine($"Catalog operation stopped ({exception.GetType().Name}). Check inputs, migration state and database confirmation.");
    return 1;
}
