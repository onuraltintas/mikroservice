using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

var connection = Environment.GetEnvironmentVariable("SPEED_MAINTENANCE_DRILL_CONNECTION")
    ?? throw new InvalidOperationException("Named clone connection required.");
if (new NpgsqlConnectionStringBuilder(connection).Database != "speed_maintenance_drill_20261015")
    throw new InvalidOperationException("Verification may run only against the named disposable clone.");
var checks = new List<string>();
void Check(bool passed, string name) { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
await using (var repairDb = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
    .UseNpgsql(connection, options => options.MigrationsHistoryTable("__ef_migrations_history", "speed_reading")).Options))
{
    var pending = (await repairDb.Database.GetPendingMigrationsAsync()).ToArray();
    Check(pending.SequenceEqual(new[] { "20261015163000_ClearUnmeasuredAssignmentMetrics" }), "only-approved-migration-pending");
    var existing = await repairDb.StudentAssignments.AsNoTracking().ToListAsync();
    var unmeasuredIds = await repairDb.ExerciseSessionResults.Where(item => !item.IsMeasured)
        .Select(item => new { item.Id, item.StudentId }).ToListAsync();
    await repairDb.Database.MigrateAsync();
    Check(!(await repairDb.Database.GetPendingMigrationsAsync()).Any(), "migration-completed");
    var repaired = await repairDb.StudentAssignments.AsNoTracking().ToListAsync();
    Check(existing.Count == repaired.Count, "repair-memberships-preserved");
    foreach (var before in existing)
    {
        var after = repaired.Single(item => item.Id == before.Id);
        Check(after.IsCompleted == before.IsCompleted && after.CompletionDate == before.CompletionDate
            && after.ResultId == before.ResultId && after.AssignmentId == before.AssignmentId
            && after.StudentId == before.StudentId && after.IsActive == before.IsActive, "repair-history-" + before.Id);
        var shouldClear = before.IsCompleted && unmeasuredIds.Any(item => item.Id == before.ResultId && item.StudentId == before.StudentId);
        Check(shouldClear ? after.Score == null && after.KeyPerformanceMetric == null
            : after.Score == before.Score && after.KeyPerformanceMetric == before.KeyPerformanceMetric, "repair-metrics-" + before.Id);
    }
    var student = Guid.NewGuid();
    var type = ExerciseType.Create(Guid.NewGuid(), "Clone-EyeTracking-" + student, "Takip", "motion_path");
    var exercise = Exercise.Create("Clone tracking", "motion_path", """{"mode":"tracking","timing":{"durationSeconds":5},"path":{"type":"circle"}}""", 1, student, type.Id);
    var assignment = Assignment.Create(Guid.NewGuid(), exercise.Id, null, "Clone tracking assignment", "", DateTime.UtcNow.AddDays(1));
    var member = StudentAssignment.Assign(assignment.Id, student);
    repairDb.ExerciseTypes.Add(type); repairDb.Exercises.Add(exercise); repairDb.Assignments.Add(assignment); repairDb.StudentAssignments.Add(member);
    await repairDb.SaveChangesAsync();
    var service = (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [repairDb], null)!;
    var started = await service.StartAsync(student, new() { ExerciseId = exercise.Id, StudentAssignmentId = member.Id });
    await service.ValidateActionAsync(student, started.SessionId, new() { Action = "tracking_start" });
    // Disposable clone fixture only: bypass elapsed time in test data, never in live student testing.
    var session = await repairDb.ExerciseSessions.SingleAsync(item => item.Id == started.SessionId);
    var state = JsonNode.Parse(session.SessionDataJson)!;
    state["timingStartedAt"] = DateTime.UtcNow.AddSeconds(-6);
    session.SetState(state.ToJsonString()); await repairDb.SaveChangesAsync();
    var completion = await service.CompleteAsync(student, started.SessionId, new());
    Check(completion.MeasurementStatus == "NotMeasured", "tracking-not-measured");
    repairDb.ChangeTracker.Clear();
    var stored = await repairDb.StudentAssignments.SingleAsync(item => item.Id == member.Id);
    Check(stored.IsCompleted && stored.ResultId.HasValue && stored.Score == null && stored.KeyPerformanceMetric == null, "tracking-completed-without-score");
    await service.CompleteAsync(student, started.SessionId, new());
    Check(await repairDb.ExerciseSessionResults.CountAsync(item => item.SessionId == started.SessionId) == 1, "tracking-one-result");
}
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
