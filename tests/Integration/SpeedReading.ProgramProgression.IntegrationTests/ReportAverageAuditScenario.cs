using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Contracts.Reporting;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

internal static class ReportAverageAuditScenario
{
    internal static async Task Run(OwnedSpeedReadingDbContext db, string mode)
    {
        var user = Guid.NewGuid(); var start = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var text = ReadingText.Create(Guid.NewGuid(), user.ToString(), "Metin", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        if (mode == "archived")
        {
            var archived = ReadingText.Create(Guid.NewGuid(), user.ToString(), "Metin", difficultyLevel: 1);
            archived.Update(archived.Title, "Metin", "tr", null, 1, null, 1, null, false, null, 1, 5, user, start);
            db.ReadingTexts.Add(archived);
            Row(text.Id, 100, 150, start); Row(archived.Id, 0, 300, start);
        }
        else if (mode == "boundary")
        { Row(text.Id, 30, 100, start.AddSeconds(-1)); Row(text.Id, 90, 300, start); }
        else if (mode == "calendar")
        { Row(text.Id, 25, 100, start.AddHours(21.5)); Row(text.Id, 75, 300, start.AddDays(1).AddHours(.5)); }
        else
        {
            var type = ExerciseType.Create(Guid.NewGuid(), "Audit", "Audit", "focus");
            var exercise = Exercise.Create("Audit", "focus", "{}", 1, user, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            foreach (var score in new[] { 0m, 100m, 999m })
            {
                var session = ExerciseSession.Start(user, exercise.Id, null, 1, start, null); session.Complete(start.AddSeconds(60));
                db.ExerciseSessions.Add(session);
                db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user, exercise.Id,
                    null, 0, 60, 0, 0, 0, score, start.AddSeconds(60), isMeasured: score != 999));
            }
        }
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().Where(r => r.UserId == user).OrderBy(r => r.Id).ToListAsync())
            + JsonSerializer.Serialize(await db.ExerciseSessionResults.AsNoTracking().Where(r => r.StudentId == user).OrderBy(r => r.Id).ToListAsync());
        var analytics = ExerciseReportingScenario.Analytics(db); var end = start.AddDays(mode == "calendar" ? 2 : 1);
        var summary = await analytics.GetStudentSummaryAsync(user, start, end);
        var scope = new SpeedReadingTeacherStudentScopeResponse([], [user], 1);
        var reports = ExerciseReportingScenario.Reports(db);
        var content = await reports.GetContentAnalysisAsync(scope, start, end);
        if (mode == "boundary")
        {
            Require((await analytics.GetStudentReadingSpeedAsync(user, start, end)).ImprovementRate == 200, "Boundary reading counted in both speed periods");
            Require((await analytics.GetStudentComprehensionAsync(user, start, end)).ImprovementRate == 200, "Boundary reading counted in both comprehension periods");
        }
        else if (mode == "calendar")
        {
            Require(summary.Daily.Count == 1 && summary.Daily[0].Date == new DateOnly(2040, 1, 2)
                && summary.Daily[0].AverageWpm == 200 && summary.Daily[0].AverageComprehension == 50, "Daily averages must share the Istanbul calendar day");
            var activity = await analytics.GetStudentActivityAsync(user, start, end);
            Require(activity.Heatmap.Single(p => p.Value > 0).Value == 2 && activity.StudyTime.MostActiveHour == 0, "Activity buckets use a different calendar");
            Require(content.ReadingPerformanceChart.Count == 1 && content.ReadingPerformanceChart[0].Name == "2040-01-02", "Institution/teacher trend calendar differs");
        }
        else if (mode == "archived")
        {
            var overview = await reports.GetClassOverviewAsync(scope, start, end);
            Require(summary.AverageComprehension == 50 && overview.ClassAverageComprehension == 50, "Summary averages changed");
            Require(content.ReadingAnalysis.Single().AverageComprehension == 50 && content.ReadingAnalysis.Single().AverageWpm == 225,
                "Archived texts must retain their historical measurements");
            Require((await analytics.GetStudentComprehensionAsync(user, start, end)).Categories.Single().Value == 50,
                "Student category average excluded archived history");
        }
        else
        {
            Require(summary.ExercisesCompleted == 3 && summary.AverageSuccessRate == 50,
                "Unmeasured exercises or real zero scores changed the success average");
            Require(content.ExerciseAnalysis.Single().AverageScore == 50, "Teacher/institution success average differs from student");
        }
        var after = JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().Where(r => r.UserId == user).OrderBy(r => r.Id).ToListAsync())
            + JsonSerializer.Serialize(await db.ExerciseSessionResults.AsNoTracking().Where(r => r.StudentId == user).OrderBy(r => r.Id).ToListAsync());
        Require(before == after, "Reporting changed saved scores");

        void Row(Guid textId, decimal score, int wpm, DateTime date) => db.ReadingSessions.Add(ReadingSession.Import(
            Guid.NewGuid(), user, textId, 60, wpm, 1, 4, score, 0, date, date, null, null, null, true));
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
