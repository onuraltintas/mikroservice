using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using EduPlatform.Shared.Contracts.Reporting;

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
        db.ReadingSessions.Add(Row(user, 180, true, now.AddDays(-2), 1, 100));
        if (mode != "empty")
            db.ReadingSessions.Add(Row(user, mode == "measured" ? 150 : mode == "unmeasured" ? 999 : 0,
                mode is "measured" or "zero", now, mode is "measured" or "comprehension-only" ? 1 : 0));
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().OrderBy(r => r.Id).ToListAsync());
        var analytics = ExerciseReportingScenario.Analytics(db);
        var speed = await analytics.GetStudentReadingSpeedAsync(user, now.AddDays(-1), now.AddDays(1));
        var comprehension = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1));
        var summary = await analytics.GetStudentSummaryAsync(user, now.AddDays(-1), now.AddDays(1));
        Require(summary.ComprehensionDataAvailable == (mode is "measured" or "comprehension-only"), "Summary must distinguish a real zero from missing comprehension");
        var content = await ExerciseReportingScenario.Reports(db).GetContentAnalysisAsync(
            new SpeedReadingTeacherStudentScopeResponse([], [user], 1), now.AddDays(-1), now.AddDays(1));
        Require(content.ReadingAnalysis.All(row => row.ComprehensionDataAvailable == (mode is "measured" or "comprehension-only")), "Content rows must distinguish missing comprehension");
        if (mode == "measured")
        {
            Require(speed.AverageWpm == 150 && speed.Recommendations.Count > 0, "Real speed must retain its measurement and advice");
            Require(speed.Benchmark.PerformanceLevel != "Veri yok", "Real speed must remain comparable");
        }
        else
        {
            Require(speed.Benchmark.PerformanceLevel == "Veri yok", "No speed measurement must not receive a performance classification");
            Require(speed.Recommendations.Count == 0, "No speed measurement must not receive recommendations");
            Require(speed.ImprovementRate == 0, "Missing speed must not become a false decline from the previous period");
            Require(speed.Trend.Count == 0 && speed.AverageWpm == 0, "Unmeasured or invalid speed leaked into analytics");
            Require(content.ReadingPerformanceChart.All(row => row.Series.All(series => series.Name != "WPM")), "Content charts must not plot a fake zero speed");
        }
        if (mode is "measured" or "comprehension-only")
            Require(comprehension.TotalQuestionsAttempted == 1 && comprehension.AverageComprehension == 0
                && comprehension.Benchmark.PerformanceLevel != "Veri yok", "A measured zero comprehension score must remain a measured zero");
        else
        {
            Require(comprehension.Benchmark.PerformanceLevel == "Veri yok" && comprehension.WeakAreas.Count == 0,
                "No comprehension measurement must not receive a performance classification or advice");
            Require(comprehension.ImprovementRate == 0, "Missing comprehension must not become a false decline from the previous period");
        }
        Require(before == JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().OrderBy(r => r.Id).ToListAsync()), "Reports changed saved measurements");

        ReadingSession Row(Guid owner, int wpm, bool measured, DateTime date, int questions = 0, decimal comprehensionRate = 0) =>
            ReadingSession.Import(Guid.NewGuid(), owner, text.Id, 60, wpm, comprehensionRate > 0 ? 1 : 0, questions, comprehensionRate, 0, date, date, null, null, null, measured);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
