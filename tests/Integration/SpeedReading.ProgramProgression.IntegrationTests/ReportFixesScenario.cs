using EduPlatform.Shared.Contracts.Reporting;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.ProgramProgression.IntegrationTests;

// H22 (report audit 30.10.2026): the rewritten admin and institution report queries run on PostgreSQL.
internal static class ReportFixesScenario
{
    internal static async Task Run(OwnedSpeedReadingDbContext db)
    {
        var institution = Guid.NewGuid(); var student = Guid.NewGuid(); var now = DateTime.UtcNow;
        var joined = now.AddDays(-5);
        var type = ExerciseType.Create(Guid.NewGuid(), "SchulteTable", "Schulte", "grid_interaction");
        var exercise = Exercise.Create("Schulte", "grid_interaction", "{}", 1, student, type.Id);
        var text = ReadingText.Create(Guid.NewGuid(), student.ToString(), "Metin", difficultyLevel: 1);
        db.ExerciseTypes.Add(type); db.Exercises.Add(exercise); db.ReadingTexts.Add(text);
        db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(institution, student,
            SpeedReadingInstitutionMemberRole.Student, student, joined));
        db.ReadingSessions.AddRange(
            Reading(now.AddDays(-8), 500, 20),   // before joining: never in institution reports
            Reading(now.AddHours(-2), 200, 80));
        foreach (var measured in new[] { true, false })
        {
            var session = ExerciseSession.Start(student, exercise.Id, null, 1, now.AddHours(-1).AddMinutes(-1), null);
            db.ExerciseSessions.Add(session);
            db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session.Id, student, exercise.Id, null, 0, 60, 0, 0, 0,
                measured ? 90 : 0, now.AddHours(-1), isMeasured: measured));
        }
        await db.SaveChangesAsync();

        var admin = (ILegacySpeedReadingAdminAnalytics)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly
            .GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdminAnalytics")!, db,
            new Institutions(institution), new Users())!;
        var from = now.AddDays(-30); var to = now.AddDays(1);
        var usage = await admin.GetPlatformUsageAsync(from, to);
        Require(usage.FeatureUsageStats["exercise"] == 2 && usage.FeatureUsageStats["reading"] == 2, "Platform counts");
        var content = await admin.GetContentAnalysisAsync(from, to);
        Require(content.ExerciseAnalysis.Single().AverageScore == 90, "Measured-only type average");
        var health = await admin.GetSystemHealthAsync(from, to);
        Require(health.TotalExercisesCompleted == 2, "Health exercise count");
        var institutions = await admin.GetInstitutionAnalyticsAsync(from, to);
        var row = institutions.InstitutionComparison.Single();
        Require(row.AverageComprehension == 50 && row.AveragePerformance == 50 && row.AverageWpm == 350, "Institution averages");
        var day = DateTime.SpecifyKind(SpeedReadingReportCalendar.LocalTime(now).Date, DateTimeKind.Unspecified);
        Require((await admin.GetPlatformUsageAsync(day, day)).TotalActivities >= 1, "Today is included in a date-only range");

        var scope = new SpeedReadingTeacherStudentScopeResponse([institution], [], 1, []);
        var reports = ExerciseReportingScenario.Reports(db);
        var overview = await reports.GetClassOverviewAsync(scope, from, to);
        Require(overview.ClassAverageWpm == 200 && overview.TotalActivitiesCompleted == 3, "Membership floor in overview");
        var time = await reports.GetTimeProgressAsync(scope, from, to);
        Require(time.WeeklyProgressChart.Sum(item => item.Series.Sum(series => series.Value)) == 3, "Membership floor in time report");

        ReadingSession Reading(DateTime at, int wpm, decimal comprehension) => ReadingSession.Import(Guid.NewGuid(), student, text.Id,
            60, wpm, (int)(comprehension / 20), 5, comprehension, 0, at, at, null, null, null);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    private sealed class Institutions(Guid id) : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingInstitutionScopeResponse([new SpeedReadingInstitutionScopeItem(id, "Okul", true)]));
    }

    private sealed class Users : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> ids, CancellationToken token = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }
}
