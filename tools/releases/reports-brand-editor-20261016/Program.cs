using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.ProgramProgression.IntegrationTests;

var readOnly = args.SequenceEqual(new[] { "--production-read-only" });
if (args.Length > 0 && !readOnly) throw new InvalidOperationException("Unknown operation");
var connection = Environment.GetEnvironmentVariable("REPORTS_CONNECTION") ?? throw new InvalidOperationException("Missing connection");
var database = new NpgsqlConnectionStringBuilder(connection).Database;
if (database != (readOnly ? "speedreading_owned_db" : "reports_brand_editor_drill_20261016")) throw new InvalidOperationException("Unexpected database");
var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection,
    builder => builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading")).Options;
await using var db = new OwnedSpeedReadingDbContext(options);
if ((await db.Database.GetPendingMigrationsAsync()).Any() || db.Database.HasPendingModelChanges())
    throw new InvalidOperationException("Unexpected schema change");
if (await db.UserGamifications.GroupBy(s => s.UserId).AnyAsync(g => g.Count() > 1)) throw new InvalidOperationException("Duplicate points row");
var before = await Fingerprints(connection);
if (!readOnly)
{
    await ExerciseReportingScenario.Run(db);
    foreach (var mode in new[] { "empty", "unmeasured", "zero", "measured", "comprehension-only" })
        await UnmeasuredReportScenario.Run(db, mode);
    await InstitutionBenchmarkScenario.Run(db);
    foreach (var mode in new[] { "multi", "zero", "absent" })
        await InstitutionBenchmarkScenario.Boundaries(db, mode);
    foreach (var mode in new[] { "archived", "boundary", "calendar", "success", "series-log", "series-fallback" })
        await ReportAverageAuditScenario.Run(db, mode);
    var after = await Fingerprints(connection);
    foreach (var (table, rows) in before)
        foreach (var (hash, count) in rows)
            if (!after[table].TryGetValue(hash, out var remaining) || remaining < count)
                throw new InvalidOperationException($"Existing row changed: {table}");
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, readOnly, pendingMigrations = 0,
    preservedTables = before.Count, originalRows = before.Values.Sum(rows => rows.Values.Sum()),
    fingerprints = readOnly ? before : null, scenarios = readOnly ? 0 : 16 }));

static async Task<Dictionary<string, Dictionary<string, long>>> Fingerprints(string connection)
{
    await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
    await using var transaction = await sql.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
    await using (var readOnly = new NpgsqlCommand("SET TRANSACTION READ ONLY", sql, transaction)) await readOnly.ExecuteNonQueryAsync();
    var tables = new List<string>();
    await using (var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='speed_reading' AND tablename NOT IN ('__EFMigrationsHistory', '__ef_migrations_history') ORDER BY tablename", sql, transaction))
    await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
    var result = new Dictionary<string, Dictionary<string, long>>(); var quote = new NpgsqlCommandBuilder();
    foreach (var table in tables)
    {
        var hashes = new Dictionary<string, long>();
        await using var command = new NpgsqlCommand($"SELECT md5(to_jsonb(t)::text), count(*) FROM speed_reading.{quote.QuoteIdentifier(table)} t GROUP BY 1", sql, transaction);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) hashes.Add(reader.GetString(0), reader.GetInt64(1));
        result.Add(table, hashes);
    }
    await transaction.CommitAsync(); return result;
}
