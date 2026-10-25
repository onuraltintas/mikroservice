using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

var connection = Environment.GetEnvironmentVariable("SPEED_MAINTENANCE_DRILL_CONNECTION")
    ?? throw new InvalidOperationException("Named clone connection required.");
if (new NpgsqlConnectionStringBuilder(connection).Database != "speed_maintenance_drill_20261015")
    throw new InvalidOperationException("Verification may run only against the named disposable clone.");
var checks = new List<string>();
void Check(bool passed, string name) { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
await using (var rosterDb = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection).Options))
{
    var teacher = Guid.NewGuid();
    var roster = new OwnedSpeedReadingTeacherStudentAssignments(rosterDb, new EligibleMembers());
    Check((await roster.GetTeacherRosterAsync(teacher, [], 1, 25)).TotalCount == 0, "standalone-empty-roster");
    Check((await roster.GetTeacherRosterAsync(teacher, [Guid.NewGuid()], 1, 25)).TotalCount == 0, "institution-empty-roster");
}
foreach (var (mode, learnThenQuiz) in new[] { ("learning", false), ("review", false), ("quiz", false), ("learning", true) })
{
    await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection).Options);
    // Clone only: the deployment script drops the entire disposable database after verification.
    await db.Database.ExecuteSqlRawAsync("UPDATE speed_reading.achievements SET \"IsActive\" = false");
    var student = Guid.NewGuid();
    var type = ExerciseType.Create(Guid.NewGuid(), "Maintenance-Vocabulary-" + student, "Doğrulama", "vocabulary_builder");
    var exercise = Exercise.Create("Maintenance vocabulary", "vocabulary_builder", JsonSerializer.Serialize(new {
        engineType = "vocabulary_builder", engineConfig = new { mode, learnThenQuiz, vocabulary = new { count = 4, difficultyLevel = 2, requireFullPool = true } }
    }), 2, student, type.Id);
    db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
    db.Achievements.Add(Achievement.Create(Guid.NewGuid(), "Maintenance badge", "Second box", "vocabulary", "bronze", null, "",
        "vocabulary_box", """{"box":2}""", null, null, false, 25, true, 0, student, DateTime.UtcNow));
    for (var i = 0; i < 4; i++)
    {
        var word = VocabularyItem.Create(Guid.NewGuid(), "Kelime" + i, "Anlam" + i, null, null, null, "Genel", 2, null, student, DateTime.UtcNow);
        db.VocabularyItems.Add(word);
        if (mode == "review") db.UserVocabularyProgresses.Add(UserVocabularyProgress.Create(Guid.NewGuid(), student, word.Id, DateTime.UtcNow.AddDays(-2)));
    }
    await db.SaveChangesAsync();
    var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [db], null)!;
    var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
    var selected = started.InitialData.GetProperty("vocabularyWords");
    var label = mode + (learnThenQuiz ? "-then-quiz" : "");
    Check(selected.GetArrayLength() == 4, label + "-four-words");
    if (learnThenQuiz)
    {
        foreach (var word in selected.EnumerateArray()) await service.ValidateActionAsync(student, started.SessionId, new() {
            ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
                ["vocabularyItemId"] = word.GetProperty("id").Clone(), ["reviewKind"] = JsonSerializer.SerializeToElement("known") }
        });
        db.ChangeTracker.Clear();
        var resumed = await service.StartAsync(student, new() { ExerciseId = exercise.Id });
        Check(resumed.SessionId == started.SessionId && resumed.InitialData.GetProperty("vocabularyMode").GetString() == "quiz", label + "-resume");
    }
    foreach (var word in selected.EnumerateArray())
    {
        var action = new ExerciseActionRequest { ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
            ["vocabularyItemId"] = word.GetProperty("id").Clone(), ["reviewKind"] = JsonSerializer.SerializeToElement(mode == "quiz" || learnThenQuiz ? "quiz" : "known"),
            ["questionType"] = word.GetProperty("questionType").Clone(),
            ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word").Clone() } };
        Check((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid, label + "-answer");
        Check((await service.ValidateActionAsync(student, started.SessionId, action)).IsValid, label + "-replay");
    }
    var result = await service.CompleteAsync(student, started.SessionId, new());
    db.ChangeTracker.Clear();
    Check(result.Accuracy == 100m && result.RawWPM == null && result.WordsRead == null, label + "-result");
    Check(await db.UserVocabularyProgresses.CountAsync(x => x.UserId == student) == 4, label + "-four-reviews");
    await service.CompleteAsync(student, started.SessionId, new()); db.ChangeTracker.Clear();
    Check(await db.ExerciseSessionResults.CountAsync(x => x.SessionId == started.SessionId) == 1, label + "-one-completion");
    var stats = await db.UserGamifications.Where(x => x.UserId == student).ToListAsync();
    if (mode == "quiz" || learnThenQuiz)
    {
        Check(stats.Count == 1 && stats[0].TotalVocabularyQuestionsAnswered == 4 && stats[0].TotalExercisesCompleted == 1, label + "-one-profile");
        Check(stats[0].TotalXP == 25L + result.XpGained, label + "-xp-once");
        Check(await db.UserAchievements.CountAsync(x => x.UserId == student) == 1, label + "-badge-once");
    }
    else Check(stats.Count == 0 && !await db.UserAchievements.AnyAsync(x => x.UserId == student), label + "-no-gamification");
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = checks.Count, checks }));

sealed class EligibleMembers : ISpeedReadingInstitutionMemberEligibility
{
    public Task<bool> IsEligibleAsync(Guid userId, SpeedReadingInstitutionMemberRole role, CancellationToken cancellationToken = default) => Task.FromResult(true);
}
