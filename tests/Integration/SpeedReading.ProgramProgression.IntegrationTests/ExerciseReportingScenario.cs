using System.Text.Json;
using EduPlatform.Shared.Contracts.Reporting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

internal static class ExerciseReportingScenario
{
    internal static readonly string[] Types = ["SchulteTable", "Tachistoscope", "SpeedReading", "Comprehension",
        "Scanning", "Chunking", "TextFade", "VisualExpansion", "Fixation", "Saccade", "RegressionReduction",
        "Subvocalization", "Visualization", "Focus", "Vocabulary", "FreeReading", "ErrorAnalysis", "RSVP",
        "Skimming", "EyeTracking", "ExamSimulation"];

    internal static async Task Run(OwnedSpeedReadingDbContext db)
    {
        var user = Guid.NewGuid(); var other = Guid.NewGuid(); var institution = Guid.NewGuid();
        var now = DateTime.UtcNow; var prefix = user.ToString("N"); var progress = Guid.NewGuid();
        var template = ProgramTemplate.Import(Guid.NewGuid(), prefix + "Program", "", Guid.NewGuid(), 0, 100,
            "{}", 1, 2, 5, 1, 3, true, 1, 0, null, false, now, null, null, null);
        db.ProgramTemplates.Add(template);
        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(progress, user, template, 0, 0, user, now));
        var exerciseIds = new List<Guid>(); var sessionIds = new List<Guid>();
        foreach (var name in Types)
        {
            var type = ExerciseType.Create(Guid.NewGuid(), prefix + name, name, name);
            var exercise = Exercise.Create(prefix + name, name, "{}", 1, user, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); exerciseIds.Add(exercise.Id);
            var session = ExerciseSession.Start(user, exercise.Id, null, 1, now.AddMinutes(-1), null);
            session.Complete(now); db.ExerciseSessions.Add(session); sessionIds.Add(session.Id);
            var measured = name != "EyeTracking";
            db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user, exercise.Id,
                null, 0, 60, 0, 0, 0, measured ? 100 : 0, now, isMeasured: measured));
            if (name is "Vocabulary" or "SpeedReading" or "EyeTracking") db.DailyExerciseLogs.Add(Log(user, progress, exercise, now, session.Id, measured));
            if (name == "SpeedReading")
            {
                var text = ReadingText.Create(Guid.NewGuid(), "Okuma", "Metin", difficultyLevel: 1);
                db.ReadingTexts.Add(text);
                db.ReadingSessions.Add(ReadingSession.Import(session.Id, user, text.Id, 60, 240, 1, 1, 100, 240, now, now, null, null, null));
            }
        }
        // Results outside the requested date/student/assessment scope must not leak into the report.
        foreach (var (owner, date, assessment) in new[] { (other, now, false), (user, now.AddDays(-20), false), (user, now.AddDays(20), false), (user, now, true) })
        {
            var excluded = ExerciseSession.Start(owner, exerciseIds[0], null, 1, date.AddMinutes(-1), null);
            excluded.Complete(date); db.ExerciseSessions.Add(excluded);
            db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), excluded.Id, owner, exerciseIds[0], null, 0, 60, 0, 0, 0, 100, date, isAssessmentMode: assessment));
        }
        db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(institution, user, SpeedReadingInstitutionMemberRole.Student, user, now));
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.ExerciseSessionResults.AsNoTracking().OrderBy(r => r.Id).ToListAsync());
        var start = now.AddDays(-1); var end = now.AddDays(1); var analytics = Analytics(db);
        var summary = await analytics.GetStudentSummaryAsync(user, start, end);
        Require(summary.ReadingSessions + summary.ExercisesCompleted == Types.Length, "Every result must be counted once in student summary");
        var activity = await analytics.GetStudentActivityAsync(user, start, end);
        Require(activity.StudyTime.TotalSessions == Types.Length && activity.RecentActivities.Count == Types.Length, "Student activity missing or duplicating results");
        var word = activity.RecentActivities.Single(a => a.ContentTitle == prefix + "Vocabulary");
        Require(word.IsMeasured && word.SuccessRate == 100 && word.Wpm is null && word.Comprehension is null, "Vocabulary must show score without reading metrics");
        var eye = activity.RecentActivities.Single(a => a.ContentTitle == prefix + "EyeTracking");
        Require(!eye.IsMeasured && eye.SuccessRate is null, "Unmeasured activity must not display a zero score");
        var reports = Reports(db);
        foreach (var scope in new[] { new SpeedReadingTeacherStudentScopeResponse([], [user], 1), new SpeedReadingTeacherStudentScopeResponse([institution], [], 1) })
        {
            Require((await reports.GetClassOverviewAsync(scope, start, end)).TotalActivitiesCompleted == Types.Length, "Staff overview must match student totals");
            var content = await reports.GetContentAnalysisAsync(scope, start, end);
            Require(content.ExerciseAnalysis.Count == Types.Length && content.ExerciseAnalysis.All(t => t.TotalCompletions == 1), "Missing or duplicated exercise type in staff content report");
            Require(content.ExerciseAnalysis.Single(t => t.ExerciseTypeName == "EyeTracking").PerformanceLevel == "not-measured", "Unmeasured content must be labelled");
            var time = await reports.GetTimeProgressAsync(scope, start, end);
            Require(time.WeeklyProgressChart.Sum(p => p.Series.Sum(s => s.Value)) == Types.Length, "Staff time report must match totals");
        }
        Require(before == JsonSerializer.Serialize(await db.ExerciseSessionResults.AsNoTracking().OrderBy(r => r.Id).ToListAsync()), "Reporting changed saved results");
    }

    internal static async Task Boundaries(OwnedSpeedReadingDbContext db, bool sameDisplayName)
    {
        var user = Guid.NewGuid(); var now = DateTime.UtcNow; var prefix = user.ToString("N");
        foreach (var (suffix, score) in new[] { ("A", 65m), ("B", 0m) })
        {
            var type = ExerciseType.Create(Guid.NewGuid(), prefix + suffix, sameDisplayName ? "Aynı ad" : suffix, suffix);
            var exercise = Exercise.Create(prefix + suffix, suffix, "{}", 1, user, type.Id);
            db.ExerciseTypes.Add(type); db.Exercises.Add(exercise);
            var session = ExerciseSession.Start(user, exercise.Id, null, 1, now.AddMinutes(-1), null);
            session.Complete(now); db.ExerciseSessions.Add(session);
            db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, user, exercise.Id,
                null, 0, 60, 0, 0, 0, score, now));
        }
        await db.SaveChangesAsync();
        if (sameDisplayName)
        {
            var content = await Reports(db).GetContentAnalysisAsync(new SpeedReadingTeacherStudentScopeResponse([], [user], 1), now.AddDays(-1), now.AddDays(1));
            Require(content.ExerciseAnalysis.Count == 2 && content.ExerciseAnalysis.All(t => t.TotalCompletions == 1), "Different type IDs must not merge when display names match");
        }
        else
        {
            var activity = await Analytics(db).GetStudentActivityAsync(user, now.AddDays(-1), now.AddDays(1));
            Require(activity.RecentActivities.All(a => !a.IsPassed), "Exercise scores below 70 must not be reported as passed");
            Require(activity.RecentActivities.Single(a => a.ContentTitle == prefix + "B").SuccessRate == 0, "Measured zero must remain zero");
        }
    }

    internal static DailyExerciseLog Log(Guid user, Guid progress, Exercise exercise, DateTime now, Guid? session, bool measured = true) =>
        DailyExerciseLog.Import(Guid.NewGuid(), user, progress, exercise.Id, exercise.ExerciseTypeId, 1, 1, 1, now,
            60, measured ? 100 : 0, measured, "{}", 1, false, "verification", 1, 0, 1, null, null, 4, TimeSpan.Zero,
            0, 0, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, now, null, null, null, isMeasured: measured, sessionId: session);
    internal static ILegacySpeedReadingAnalytics Analytics(OwnedSpeedReadingDbContext db) =>
        (ILegacySpeedReadingAnalytics)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAnalytics")!, db, new MemoryCache(new MemoryCacheOptions()))!;
    internal static ILegacySpeedReadingTeacherReports Reports(OwnedSpeedReadingDbContext db) =>
        (ILegacySpeedReadingTeacherReports)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingTeacherReports")!, db, new Directory())!;
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private sealed class Directory : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken token = default) => Task.FromResult(new SpeedReadingUserDirectoryResponse([]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken token = default) => Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
