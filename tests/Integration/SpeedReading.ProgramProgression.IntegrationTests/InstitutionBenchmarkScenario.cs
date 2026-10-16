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

    internal static async Task Boundaries(OwnedSpeedReadingDbContext db, string mode)
    {
        // Keep each synthetic platform cohort separate when replayed on a copy of the live database.
        var now = DateTime.UtcNow.AddYears(10).AddDays(mode == "multi" ? 0 : mode == "zero" ? 7 : 14);
        var user = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var text = ReadingText.Create(Guid.NewGuid(), user.ToString(), "Metin", difficultyLevel: 1);
        db.ReadingTexts.Add(text);
        if (mode != "absent") Member(a, user);
        if (mode == "multi") Member(b, user);
        db.ReadingSessions.Add(Row(user, mode == "multi" ? 80 : 0, 200));
        if (mode == "multi")
        {
            var peerA = Guid.NewGuid(); var peerB = Guid.NewGuid(); Member(a, peerA); Member(b, peerB);
            db.ReadingSessions.Add(Row(peerA, 100, 300)); db.ReadingSessions.Add(Row(peerB, 20, 100));
            // Two readings from peer A: institution averages must weight readings, not students.
            db.ReadingSessions.Add(Row(peerA, 100, 300));
        }
        await db.SaveChangesAsync();
        var analytics = ExerciseReportingScenario.Analytics(db);
        var report = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1));
        Require(report.Benchmark.InstitutionDataAvailable == (mode == "zero"), "Ambiguous or absent institution must not appear as measured zero");
        Require(report.Benchmark.PlatformDataAvailable, "A real zero platform average is still a measurement");
        if (mode != "multi")
            Require(report.Benchmark.PerformanceLevel == "Average", "A real zero must be compared to the available zero benchmark");
        else
        {
            var reportA = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1), a, default);
            var reportB = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1), b, default);
            Require(Math.Abs(reportA.Benchmark.InstitutionAverage - 280m / 3) < .001m && reportB.Benchmark.InstitutionAverage == 50,
                "Selected institutions or their cached benchmarks leaked into each other");
            var speedA = await analytics.GetStudentReadingSpeedAsync(user, now.AddDays(-1), now.AddDays(1), a, default);
            var speedB = await analytics.GetStudentReadingSpeedAsync(user, now.AddDays(-1), now.AddDays(1), b, default);
            Require(Math.Abs(speedA.Benchmark.InstitutionAverage - 800m / 3) < .001m && speedB.Benchmark.InstitutionAverage == 150,
                "Speed comparison ignored the selected institution");
        }
        var unrelated = await analytics.GetStudentComprehensionAsync(user, now.AddDays(-1), now.AddDays(1), Guid.NewGuid(), default);
        Require(!unrelated.Benchmark.InstitutionDataAvailable, "Non-member must not receive an institution benchmark");
        var empty = await analytics.GetStudentComprehensionAsync(user, now.AddDays(2), now.AddDays(3));
        Require(!empty.Benchmark.InstitutionDataAvailable && !empty.Benchmark.PlatformDataAvailable
            && empty.Benchmark.PerformanceLevel == "Veri yok", "Empty interval must remain unmeasured");

        void Member(Guid institution, Guid owner) => db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(
            institution, owner, SpeedReadingInstitutionMemberRole.Student, user, now));
        ReadingSession Row(Guid owner, decimal score, int wpm) => ReadingSession.Import(Guid.NewGuid(), owner,
            text.Id, 60, wpm, (int)(score / 20), 5, score, 0, now, now, null, null, null, true);
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
