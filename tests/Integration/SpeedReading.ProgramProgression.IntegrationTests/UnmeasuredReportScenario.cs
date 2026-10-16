using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

internal static class UnmeasuredReportScenario
{
    internal static async Task Run(OwnedSpeedReadingDbContext db, string mode)
    {
        var user = Guid.NewGuid(); var now = DateTime.UtcNow;
        var text = ReadingText.Create(Guid.NewGuid(), user.ToString(), "Metin", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        // Another student's real measurement and this student's old measurement cannot fill today's gap.
        db.ReadingSessions.Add(Row(Guid.NewGuid(), 300, true, now));
        db.ReadingSessions.Add(Row(user, 180, true, now.AddDays(-20)));
        if (mode != "empty")
            db.ReadingSessions.Add(Row(user, mode == "measured" ? 150 : mode == "unmeasured" ? 999 : 0,
                mode is "measured" or "zero", now, mode is "measured" or "comprehension-only" ? 1 : 0));
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().OrderBy(r => r.Id).ToListAsync());
        var analytics = ExerciseReportingScenario.Analytics(db);
        var speed = await analytics.GetStudentReadingSpeedAsync(user, now.AddDays(-1), now.AddDays(1));
        var comprehension = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1));
        if (mode == "measured")
        {
            Require(speed.AverageWpm == 150 && speed.Recommendations.Count > 0, "Real speed must retain its measurement and advice");
            Require(speed.Benchmark.PerformanceLevel != "Veri yok", "Real speed must remain comparable");
        }
        else
        {
            Require(speed.Benchmark.PerformanceLevel == "Veri yok", "No speed measurement must not receive a performance classification");
            Require(speed.Recommendations.Count == 0, "No speed measurement must not receive recommendations");
            Require(speed.Trend.Count == 0 && speed.AverageWpm == 0, "Unmeasured or invalid speed leaked into analytics");
        }
        if (mode is "measured" or "comprehension-only")
            Require(comprehension.TotalQuestionsAttempted == 1 && comprehension.AverageComprehension == 0
                && comprehension.Benchmark.PerformanceLevel != "Veri yok", "A measured zero comprehension score must remain a measured zero");
        else
            Require(comprehension.Benchmark.PerformanceLevel == "Veri yok" && comprehension.WeakAreas.Count == 0,
                "No comprehension measurement must not receive a performance classification or advice");
        Require(before == JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().OrderBy(r => r.Id).ToListAsync()), "Reports changed saved measurements");

        ReadingSession Row(Guid owner, int wpm, bool measured, DateTime date, int questions = 0) =>
            ReadingSession.Import(Guid.NewGuid(), owner, text.Id, 60, wpm, 0, questions, 0, 0, date, date, null, null, null, measured);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
