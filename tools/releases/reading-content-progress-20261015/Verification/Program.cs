using System.Reflection;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Profiles;
using SpeedReading.Infrastructure.Persistence;

const string database = "reading_content_progress_drill_20261015";
var connection = Environment.GetEnvironmentVariable("READING_PROGRESS_DRILL_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection");
if (new NpgsqlConnectionStringBuilder(connection).Database != database) throw new InvalidOperationException("Only the named disposable database is allowed");
var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection).Options;
var tested = new List<object>(); var failures = new List<object>();
await using var catalogue = new OwnedSpeedReadingDbContext(options);
var types = new[] { "Chunking", "SpeedReading", "TextFading", "RegressionReduction", "SubvocalizationReduction", "RSVP" };
var exercises = await (from exercise in catalogue.Exercises.AsNoTracking()
    join type in catalogue.ExerciseTypes.AsNoTracking() on exercise.ExerciseTypeId equals type.Id
    where Enumerable.Contains(types, type.Name) && exercise.IsActive && !exercise.IsDeleted && !exercise.IsAssessmentOnly
        && type.IsActive && !type.IsDeleted && exercise.TargetAgeGroupId != null
    orderby type.Name, exercise.TargetAgeGroupId, exercise.DifficultyLevel
    select new { exercise.Id, exercise.Title, exercise.TargetAgeGroupId, exercise.DifficultyLevel, Type = type.Name }).ToListAsync();
if (exercises.Count < 80) throw new InvalidOperationException("Expected the full published reading catalogue");
var expected = JsonSerializer.Deserialize<ExpectedExercise[]>(await File.ReadAllTextAsync(
    Path.Combine(AppContext.BaseDirectory, "catalogue-expectations.json")), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
if (!types.Order().SequenceEqual(exercises.Select(item => item.Type).Distinct().Order())
    || !expected.Select(item => (item.Id, item.Type, item.AgeGroupId, item.Level)).OrderBy(item => item.Id)
        .SequenceEqual(exercises.Select(item => (item.Id, item.Type, AgeGroupId: item.TargetAgeGroupId!.Value, Level: item.DifficultyLevel)).OrderBy(item => item.Id)))
    throw new InvalidOperationException("The published type/age/level catalogue changed or is incomplete");
foreach (var exercise in exercises)
{
    await using var db = new OwnedSpeedReadingDbContext(options);
    var user = Guid.NewGuid();
    db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), user, 1, 150, 70, 15,
        exercise.TargetAgeGroupId, null, true, DateTime.UtcNow, "reading-content-progress-drill", null, null));
    await db.SaveChangesAsync();
    var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
        .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    try
    {
        var response = await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        var session = await db.ExerciseSessions.AsNoTracking().SingleAsync(item => item.Id == response.SessionId);
        var text = await db.ReadingTexts.AsNoTracking().SingleAsync(item => item.Id == session.ReadingTextId);
        if (!text.IsActive || text.IsDeleted || text.DifficultyLevel != exercise.DifficultyLevel
            || (text.TargetAgeGroupId.HasValue && text.TargetAgeGroupId != exercise.TargetAgeGroupId)
            || (text.ExerciseId.HasValue && text.ExerciseId != exercise.Id))
            throw new InvalidOperationException("Selection escaped the requested age, level or exercise");
        tested.Add(new { exercise.Id, exercise.Title, exercise.Type, exercise.DifficultyLevel,
            ageGroupId = exercise.TargetAgeGroupId, textId = text.Id });
    }
    catch (Exception error)
    {
        failures.Add(new { exercise.Id, exercise.Title, exercise.Type, error = error.Message });
    }
}
var ageMismatchChecks = 0;
foreach (var exercise in exercises.DistinctBy(item => item.Type))
{
    await using var db = new OwnedSpeedReadingDbContext(options);
    var user = Guid.NewGuid(); var wrongAge = exercises.First(item => item.TargetAgeGroupId != exercise.TargetAgeGroupId).TargetAgeGroupId;
    db.UserProfiles.Add(SpeedReadingUserProfile.Import(Guid.NewGuid(), user, 1, 150, 70, 15,
        wrongAge, null, true, DateTime.UtcNow, "reading-content-progress-drill", null, null));
    await db.SaveChangesAsync();
    var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
        .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    try
    {
        await service.StartAsync(user, new() { ExerciseId = exercise.Id });
        throw new InvalidOperationException("Wrong-age catalogue link was accepted");
    }
    catch (BusinessRuleException error) when (error.Code == "Exercise.AgeGroupMismatch")
    {
        if (await db.ExerciseSessions.AnyAsync(item => item.StudentId == user)) throw new InvalidOperationException("Rejected start wrote a session");
        ageMismatchChecks++;
    }
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = tested.Count, failed = failures.Count, ageMismatchChecks, tested, failures }));
return failures.Count == 0 ? 0 : 1;

internal sealed record ExpectedExercise(Guid Id, string Type, Guid AgeGroupId, int Level);
