using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

const string migration = "20261015202217_AddProgramDayAdvance";
var production = args.SequenceEqual(new[] { "--migrate-production" });
if (args.Length > 0 && !production) throw new InvalidOperationException("Unknown operation");
var connection = Environment.GetEnvironmentVariable("DAY_ADVANCE_CONNECTION") ?? throw new InvalidOperationException("Missing connection");
var database = new NpgsqlConnectionStringBuilder(connection).Database;
if (database != (production ? "speedreading_owned_db" : "day_advance_drill_20261015")) throw new InvalidOperationException("Unexpected database");
var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection, builder => {
    builder.EnableRetryOnFailure(); builder.MigrationsHistoryTable("__ef_migrations_history", "speed_reading");
}).Options;
await using var db = new OwnedSpeedReadingDbContext(options);
var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
if (!pending.SequenceEqual(new[] { migration })) throw new InvalidOperationException("Expected exactly the reviewed additive migration");
var before = await Fingerprints(connection);
await db.Database.MigrateAsync();
if (db.Database.HasPendingModelChanges()) throw new InvalidOperationException("Model mismatch");
var after = await Fingerprints(connection);
if (!before.OrderBy(item => item.Key).SequenceEqual(after.OrderBy(item => item.Key))) throw new InvalidOperationException("Existing data changed during migration");
if (await db.StudentProgramProgresses.AnyAsync(item => item.EarlyAdvanceDays != 0)
    || await db.ProgramProgressionPolicies.AnyAsync()) throw new InvalidOperationException("Defaults were not preserved");
if (!production)
{
    var user = Guid.NewGuid(); var now = DateTime.UtcNow;
    var exercise = await db.Exercises.FirstAsync(item => item.IsActive && !item.IsDeleted);
    var template = ProgramTemplate.Import(Guid.NewGuid(), "Disposable day advance drill", "", Guid.NewGuid(), 0, 100,
        "{}", 1, 2, 5, 1, 3, true, 1, 0, null, false, now, null, null, null);
    var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, now);
    progress.SetSchedule(JsonSerializer.Serialize(new[] {
        new { WeekNumber = 1, DayNumber = 1, Order = 1, ExerciseId = exercise.Id },
        new { WeekNumber = 1, DayNumber = 1, Order = 2, ExerciseId = exercise.Id },
        new { WeekNumber = 1, DayNumber = 2, Order = 1, ExerciseId = exercise.Id }
    }), user, now);
    progress.ApplyExerciseCompletion(80, false, 1, 2, template, user, now);
    progress.ApplyExerciseCompletion(80, false, 2, 2, template, user, now);
    db.ProgramTemplates.Add(template); db.StudentProgramProgresses.Add(progress);
    for (var slot = 1; slot <= 2; slot++) db.DailyExerciseLogs.Add(DailyExerciseLog.Import(
        Guid.NewGuid(), user, progress.Id, exercise.Id, exercise.ExerciseTypeId, 1, 1, 1, now, 60, 80, true, "{}", 1, false,
        "drill", 1, 0, 1, null, null, 4, TimeSpan.Zero, 0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0,
        now, null, null, null, slotOrder: slot));
    await db.SaveChangesAsync();
    await Service(db).UpdatePreferenceAsync(user, "allow", user);
    var request = new AdvanceProgramDayRequest(progress.Id, 1);
    async Task Advance()
    {
        await using var fresh = new OwnedSpeedReadingDbContext(options);
        await Service(fresh).AdvanceAsync(user, request);
    }
    await Task.WhenAll(Advance(), Advance());
    db.ChangeTracker.Clear();
    var saved = await db.StudentProgramProgresses.SingleAsync(item => item.Id == progress.Id);
    if (saved.EarlyAdvanceDays != 1 || saved.ExercisesCompleted != 2 || saved.DaysCompleted != 1
        || await db.DailyExerciseLogs.CountAsync(item => item.StudentProgramProgressId == progress.Id) != 2)
        throw new InvalidOperationException("Double request changed completed progress");
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = true, production, migration, preservedTables = before.Count }));

static ISpeedReadingProgramProgression Service(OwnedSpeedReadingDbContext db) =>
    (ISpeedReadingProgramProgression)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
        .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingProgramProgression")!, db, new Eligibility())!;

static async Task<Dictionary<string, string>> Fingerprints(string connection)
{
    await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
    var tables = new List<string>();
    await using (var command = new NpgsqlCommand("SELECT tablename FROM pg_tables WHERE schemaname='speed_reading' AND tablename NOT IN ('__EFMigrationsHistory', '__ef_migrations_history', 'program_progression_policies') ORDER BY tablename", sql))
    await using (var reader = await command.ExecuteReaderAsync()) while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
    var result = new Dictionary<string, string>(); var quote = new NpgsqlCommandBuilder();
    foreach (var table in tables)
    {
        var row = table == "student_program_progress" ? "to_jsonb(t) - 'EarlyAdvanceDays'" : "to_jsonb(t)";
        await using var command = new NpgsqlCommand($"SELECT coalesce(md5(string_agg(rowhash, '' ORDER BY rowhash)), 'empty') FROM (SELECT md5(({row})::text) rowhash FROM speed_reading.{quote.QuoteIdentifier(table)} t) hashes", sql);
        result[table] = (string)(await command.ExecuteScalarAsync())!;
    }
    return result;
}
internal sealed class Eligibility : ISpeedReadingInstitutionMemberEligibility
{
    public Task<bool> IsEligibleAsync(Guid user, SpeedReadingInstitutionMemberRole role, CancellationToken token = default) => Task.FromResult(true);
}
