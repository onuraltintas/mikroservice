using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.ExerciseSessions;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Gamification;
using SpeedReading.Domain.Vocabulary;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

internal static class VocabularyScoringScenario
{
    internal static async Task Run(Func<OwnedSpeedReadingDbContext> open, string mode, bool existingPoints)
    {
        var user = Guid.NewGuid(); var now = DateTime.UtcNow; var award = Guid.NewGuid();
        Guid exerciseId; string original; UserGamification? previous;
        await using (var db = open())
        {
            var type = ExerciseType.Create(Guid.NewGuid(), "Vocabulary", "Vocabulary scoring verification", "vocabulary_builder");
            var words = Enumerable.Range(0, 4).Select(i => VocabularyItem.Create(Guid.NewGuid(), "Word" + i,
                "Meaning" + i, null, null, null, "Genel", 2, null, user, now)).ToArray();
            var exercise = Exercise.Create("Vocabulary scoring verification", "vocabulary_builder", JsonSerializer.Serialize(new {
                engineType = "vocabulary_builder", engineConfig = new { mode, vocabularyItemIds = words.Select(w => w.Id),
                    vocabulary = new { count = 4, difficultyLevel = 2, requireFullPool = true } }
            }), 2, user, type.Id);
            exerciseId = exercise.Id;
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.VocabularyItems.AddRange(words);
            if (existingPoints)
            {
                var stats = UserGamification.CreateDefault(Guid.NewGuid(), user, now, user.ToString());
                stats.RecordVerifiedExerciseCompletion("SpeedReading", now.AddDays(-1), 120, 240, 85, true, 400, user, now);
                db.UserGamifications.Add(stats);
            }
            db.Achievements.Add(Achievement.Create(award, "Vocabulary verification", "Second box", "vocabulary", "bronze", null, "",
                "vocabulary_box", """{"box":2}""", null, null, false, 25, true, 0, user, now));
            await db.SaveChangesAsync();
            previous = await db.UserGamifications.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == user);
            original = JsonSerializer.Serialize(previous);
        }
        StartExerciseSessionResponse started;
        await using (var db = open()) started = await Service(db).StartAsync(user, new() { ExerciseId = exerciseId });
        foreach (var word in started.InitialData.GetProperty("vocabularyWords").EnumerateArray())
        {
            await using var db = open();
            var response = await Service(db).ValidateActionAsync(user, started.SessionId, new() {
                ActionId = Guid.NewGuid(), Action = "vocabulary_review", CustomData = new() {
                    ["vocabularyItemId"] = word.GetProperty("id"),
                    ["reviewKind"] = JsonSerializer.SerializeToElement(mode == "quiz" ? "quiz" : "known"),
                    ["questionType"] = word.GetProperty("questionType"),
                    ["selectedAnswer"] = word.GetProperty(word.GetProperty("questionType").GetString() == "word" ? "definition" : "word")
                }
            });
            Require(response.IsValid, "Valid vocabulary answer was rejected");
        }
        SpeedReading.Application.ExerciseSessions.ExerciseSessionResult result;
        await using (var db = open()) result = await Service(db).CompleteAsync(user, started.SessionId, new());
        Require(result.Accuracy == 100 && result.RawWPM is null && result.WordsRead is null, "Invalid vocabulary result");
        string completedStats;
        await using (var db = open())
        {
            Require(await db.UserVocabularyProgresses.CountAsync(s => s.UserId == user) == 4, "Vocabulary progress missing");
            var stats = await db.UserGamifications.SingleOrDefaultAsync(s => s.UserId == user);
            completedStats = JsonSerializer.Serialize(stats);
            if (mode == "learning")
            {
                Require(original == completedStats && !await db.UserAchievements.AnyAsync(s => s.UserId == user), "Learning changed points");
            }
            else
            {
                var awards = await (from unlocked in db.UserAchievements join achievement in db.Achievements
                    on unlocked.AchievementId equals achievement.Id where unlocked.UserId == user
                    select new { unlocked.AchievementId, achievement.XPReward }).ToListAsync();
                Require(awards.Count(a => a.AchievementId == award) == 1, "Achievement was not awarded exactly once");
                Require(stats is not null && stats.TotalXP == (previous?.TotalXP ?? 0) + result.XpGained + awards.Sum(a => a.XPReward), "XP lost or duplicated");
                Require(stats!.TotalVocabularyQuestionsAnswered == 4 && stats.TotalExercisesCompleted == (previous?.TotalExercisesCompleted ?? 0) + 1, "Completion counted incorrectly");
                Require(stats.MaxWPM == (previous?.MaxWPM ?? 0) && stats.TotalReadingSessionsCompleted == (previous?.TotalReadingSessionsCompleted ?? 0), "Reading history changed");
                Require(previous is null || stats.Id == previous.Id, "Existing points row replaced");
            }
        }
        await using (var db = open()) await Service(db).CompleteAsync(user, started.SessionId, new());
        await using (var db = open())
        {
            Require(completedStats == JsonSerializer.Serialize(await db.UserGamifications.AsNoTracking().SingleOrDefaultAsync(s => s.UserId == user)), "Completion replay changed points");
            Require(await db.ExerciseSessionResults.CountAsync(s => s.SessionId == started.SessionId) == 1, "Completion replay duplicated result");
        }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static ISpeedReadingExerciseSessions Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingExerciseSessions)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingExerciseSessions")!, db)!;
}
