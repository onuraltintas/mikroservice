using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using EduPlatform.Shared.Contracts.Reporting;

namespace SpeedReading.ProgramProgression.IntegrationTests;

internal static class InstitutionBenchmarkScenario
{
    internal static async Task Run(OwnedSpeedReadingDbContext db)
    {
        var user = Guid.NewGuid(); var institution = Guid.NewGuid(); var now = DateTime.UtcNow;
        var text = ReadingText.Create(Guid.NewGuid(), user.ToString(), "Metin", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(institution, user,
            SpeedReadingInstitutionMemberRole.Student, user, now));
        foreach (var score in new[] { 100m, 80m, 80m }) db.ReadingSessions.Add(Row(user, score, now));
        // Teachers, inactive students, unrelated institutions, and old readings cannot enter this cohort.
        foreach (var role in new[] { "teacher", "inactive", "other" })
        {
            var other = Guid.NewGuid();
            var membership = SpeedReadingInstitutionMembership.Create(role == "other" ? Guid.NewGuid() : institution,
                other, role == "teacher" ? SpeedReadingInstitutionMemberRole.Teacher : SpeedReadingInstitutionMemberRole.Student, user, now);
            if (role == "inactive") membership.SetActive(false, user, now);
            db.InstitutionMemberships.Add(membership); db.ReadingSessions.Add(Row(other, 0, now));
        }
        db.ReadingSessions.Add(Row(user, 0, now.AddDays(-2)));
        await db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().OrderBy(r => r.Id).ToListAsync());
        var analytics = ExerciseReportingScenario.Analytics(db);
        var report = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1));
        var overview = await ExerciseReportingScenario.Reports(db).GetClassOverviewAsync(
            new SpeedReadingTeacherStudentScopeResponse([institution], [], 1), now.AddDays(-1), now.AddDays(1));
        Require(Math.Abs(report.AverageComprehension - 260m / 3) < .001m, "Student average changed");
        Require(Math.Abs(report.Benchmark.InstitutionAverage - report.AverageComprehension) < .001m,
            $"Active membership must produce 86.67% institution average; got {report.Benchmark.InstitutionAverage}");
        Require(Math.Abs(overview.ClassAverageComprehension - report.Benchmark.InstitutionAverage) < .01m,
            "Institution overview and student comparison disagree");
        Require(before == JsonSerializer.Serialize(await db.ReadingSessions.AsNoTracking().OrderBy(r => r.Id).ToListAsync()),
            "Reports changed existing measurements");

        ReadingSession Row(Guid owner, decimal score, DateTime date) => ReadingSession.Import(Guid.NewGuid(), owner,
            text.Id, 60, 0, (int)(score / 20), 5, score, 0, date, date, null, null, null, false);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
