using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Reporting;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpeedReading.API.Controllers;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Assignments;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

// H22 (report audit 30.10.2026): report numbers, ranges and scopes.
public sealed class SpeedReadingReportFixesTests
{
    private static readonly Assembly Infrastructure = typeof(OwnedSpeedReadingDbContext).Assembly;

    [Fact]
    public async Task Series_timeline_uses_the_program_day_not_the_weekday()
    {
        await using var db = Db();
        var student = Guid.NewGuid();
        var template = Template(28);
        db.ProgramTemplates.Add(template);
        var at = DateTime.UtcNow.AddDays(-2);
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), student, template.Id, at.AddDays(-12), 2, 3, 1, 9, 60,
            at, true, null, 70, 1, 1, at.AddDays(-12), null, null, null);
        db.StudentProgramProgresses.Add(progress);
        db.DailyExerciseLogs.Add(Log(student, progress.Id, week: 2, day: 3, at));
        await db.SaveChangesAsync();

        var series = await Analytics(db).GetStudentSeriesAsync(student, at.AddDays(-5), DateTime.UtcNow);

        // Week 2, day 3 is program day 10 of 28 (35.71%), not day 3 (10.71%).
        series.CompletionTimeline.Should().ContainSingle().Which.Value.Should().Be(35.71m);
    }

    [Fact]
    public async Task Admin_date_only_ranges_cover_whole_istanbul_days_and_group_by_istanbul_time()
    {
        await using var db = Db();
        var text = Text();
        db.ReadingTexts.Add(text);
        var user = Guid.NewGuid();
        // 30.10.2026 in Istanbul (UTC+3) runs from 29.10 21:00 UTC to 30.10 21:00 UTC.
        db.ReadingSessions.AddRange(
            Reading(user, text.Id, new DateTime(2026, 10, 29, 20, 59, 0, DateTimeKind.Utc)), // 29.10 23:59 local: outside
            Reading(user, text.Id, new DateTime(2026, 10, 29, 21, 30, 0, DateTimeKind.Utc)), // 30.10 00:30 local
            Reading(user, text.Id, new DateTime(2026, 10, 30, 20, 30, 0, DateTimeKind.Utc))); // 30.10 23:30 local
        await db.SaveChangesAsync();
        var day = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Unspecified);

        var usage = await Admin(db).GetPlatformUsageAsync(day, day);

        usage.TotalReadingSessions.Should().Be(2);
        usage.DailyActiveUsers.Should().ContainSingle().Which.Name.Should().Be("2026-10-30");
        usage.HourlyActivity.Select(item => item.Name).Should().BeEquivalentTo(["00:00", "23:00"]);
    }

    [Fact]
    public async Task Admin_exercise_type_averages_use_measured_results_from_the_shared_report_source()
    {
        await using var db = Db();
        var measuredType = ExerciseType.Create(Guid.NewGuid(), "SchulteTable", "Schulte", "grid_interaction");
        var pacedType = ExerciseType.Create(Guid.NewGuid(), "Chunking", "Gruplama", "chunking");
        db.ExerciseTypes.AddRange(measuredType, pacedType);
        var schulte = Exercise.Create("Schulte", "schulte", "{}", 1, Guid.NewGuid(), measuredType.Id);
        var chunking = Exercise.Create("Gruplama", "chunking", "{}", 1, Guid.NewGuid(), pacedType.Id);
        db.Exercises.AddRange(schulte, chunking);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow.AddHours(-1);
        db.ExerciseSessionResults.AddRange(
            Result(user, schulte.Id, 80, now, measured: true),
            Result(user, schulte.Id, 0, now, measured: false),
            Result(user, chunking.Id, 0, now, measured: false));
        // A reading-flow exercise is already a reading session: it must not be counted twice.
        var text = Text();
        db.ReadingTexts.Add(text);
        var reading = Reading(user, text.Id, now);
        db.ReadingSessions.Add(reading);
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), reading.Id, user, schulte.Id, text.Id,
            200, 60, 200, 80, 0, 80, now, "[]"));
        await db.SaveChangesAsync();

        var content = await Admin(db).GetContentAnalysisAsync(null, null);
        var usage = await Admin(db).GetPlatformUsageAsync(null, null);

        var schulteRow = content.ExerciseAnalysis.Single(item => item.ExerciseTypeName == "Schulte");
        schulteRow.TotalCompletions.Should().Be(2);
        schulteRow.AverageScore.Should().Be(80);
        schulteRow.PerformanceLevel.Should().Be("medium");
        var chunkingRow = content.ExerciseAnalysis.Single(item => item.ExerciseTypeName == "Gruplama");
        chunkingRow.AverageScore.Should().BeNull();
        chunkingRow.PerformanceLevel.Should().Be("not-measured");
        usage.FeatureUsageStats["exercise"].Should().Be(3);
        usage.FeatureUsageStats["reading"].Should().Be(1);
    }

    [Fact]
    public async Task Institutions_are_ranked_by_comprehension_without_mixing_units()
    {
        await using var db = Db();
        var text = Text();
        db.ReadingTexts.Add(text);
        var careful = Guid.NewGuid(); var fast = Guid.NewGuid();
        var carefulStudent = Guid.NewGuid(); var fastStudent = Guid.NewGuid();
        var at = DateTime.UtcNow.AddDays(-30);
        db.InstitutionMemberships.AddRange(
            SpeedReadingInstitutionMembership.Create(careful, carefulStudent, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), at),
            SpeedReadingInstitutionMembership.Create(fast, fastStudent, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), at));
        db.ReadingSessions.AddRange(
            Reading(carefulStudent, text.Id, DateTime.UtcNow.AddHours(-2), wpm: 150, comprehension: 90),
            Reading(fastStudent, text.Id, DateTime.UtcNow.AddHours(-2), wpm: 450, comprehension: 60));
        await db.SaveChangesAsync();
        var directory = new InstitutionDirectory(new SpeedReadingInstitutionScopeResponse([
            new SpeedReadingInstitutionScopeItem(careful, "Dikkatli Okul", true),
            new SpeedReadingInstitutionScopeItem(fast, "Hızlı Okul", true)]));

        var result = await Admin(db, directory).GetInstitutionAnalyticsAsync(null, null);

        result.InstitutionComparison.Single(item => item.InstitutionId == careful).AveragePerformance.Should().Be(90);
        result.InstitutionComparison.Single(item => item.InstitutionId == fast).AveragePerformance.Should().Be(60);
        result.TopInstitutions.First().InstitutionName.Should().Be("Dikkatli Okul");
        result.InstitutionComparison.Single(item => item.InstitutionId == fast).AverageWpm.Should().Be(450);
    }

    [Fact]
    public async Task Results_without_questions_report_no_comprehension_and_assessment_results_are_not_listed()
    {
        await using var db = Db();
        var user = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var noQuestions = ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), user, exercise, null, 0, 60, 0, 0, 0, 90, now);
        var withQuestions = ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), user, exercise, Guid.NewGuid(), 200, 60, 200, 75, 0, 75,
            now.AddMinutes(-1), """[{"IsCorrect":true}]""");
        var textWithoutAnswers = ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), user, exercise, Guid.NewGuid(), 200, 60, 200, 0, 0, 0,
            now.AddMinutes(-2));
        var assessment = ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), user, exercise, Guid.NewGuid(), 200, 60, 200, 50, 0, 50,
            now.AddMinutes(-3), """[{"IsCorrect":false}]""", isAssessmentMode: true);
        db.ExerciseSessionResults.AddRange(noQuestions, withQuestions, textWithoutAnswers, assessment);
        await db.SaveChangesAsync();

        var page = await Progress(db).GetExerciseResultsAsync(user, 1, 20);

        page.TotalCount.Should().Be(3);
        page.Items.Should().NotContain(item => item.Id == assessment.Id);
        page.Items.Single(item => item.Id == noQuestions.Id).ComprehensionScore.Should().BeNull();
        page.Items.Single(item => item.Id == textWithoutAnswers.Id).ComprehensionScore.Should().BeNull();
        page.Items.Single(item => item.Id == withQuestions.Id).ComprehensionScore.Should().Be(75);
    }

    [Fact]
    public async Task Admin_progress_shows_the_program_day_and_question_free_logs_without_comprehension()
    {
        await using var db = Db();
        var student = Guid.NewGuid();
        var template = Template(28);
        db.ProgramTemplates.Add(template);
        var at = DateTime.UtcNow.AddDays(-40);
        var progress = StudentProgramProgress.Import(Guid.NewGuid(), student, template.Id, at, 3, 3, 1, 16, 96,
            DateTime.UtcNow, true, null, 70, 1, 1, at, null, null, null);
        db.StudentProgramProgresses.Add(progress);
        var session = Guid.NewGuid();
        var exercise = Guid.NewGuid();
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(Guid.NewGuid(), session, student, exercise, null, 0, 60, 0, 0, 0, 90,
            DateTime.UtcNow));
        db.DailyExerciseLogs.Add(Log(student, progress.Id, week: 3, day: 3, DateTime.UtcNow, session, exercise));
        await db.SaveChangesAsync();
        var scope = new SpeedReadingProgressAccessScope(Guid.NewGuid(), true, []);

        var page = await Programs(db).GetAdminStudentProgressAsync(scope, 1, 20, null);
        var details = await Programs(db).GetAdminStudentProgressDetailsAsync(scope, progress.Id);

        page.Items.Single().ProgramDay.Should().Be(17);
        page.Items.Single().TotalDays.Should().Be(28);
        details!.Progress.TotalDays.Should().Be(28);
        details.RecentLogs.Single().AverageComprehension.Should().BeNull();
        details.RecentLogs.Single().ProgramDay.Should().Be(17);
    }

    [Fact]
    public void Weekly_completion_uses_seven_day_weeks()
    {
        var row = new SpeedReadingProgramAnalyticsRow(Guid.NewGuid(), "A", "B", null, Guid.NewGuid(), "Program", 2, 4, 1, 1, 70,
            1, DateTime.UtcNow, 10, 60, true, ProgramDay: 11, TotalDays: 28);

        var result = SpeedReadingProgramAnalyticsCalculator.Calculate([row]);

        // Week 2: days 8–14; 10 completed days means 3 of 7 days of week 2 are done.
        result.WeeklyProgress.Single().CompletionRate.Should().Be(42.9m);
        result.RecentStudentProgress.Single().ProgramDay.Should().Be(11);
        result.RecentStudentProgress.Single().TotalDays.Should().Be(28);
    }

    [Fact]
    public async Task Assignment_report_includes_assignments_completed_in_the_range()
    {
        await using var db = Db();
        var teacher = Guid.NewGuid(); var student = Guid.NewGuid();
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        var old = Assignment.Create(teacher, Guid.NewGuid(), null, "Uzun ödev", null, now.AddDays(30), createdAt: now.AddDays(-60));
        db.Assignments.Add(old);
        var row = StudentAssignment.Assign(old.Id, student, assignedAt: now.AddDays(-60));
        row.Complete(Guid.NewGuid(), 85, 70, now.AddDays(-2));
        db.StudentAssignments.Add(row);
        await db.SaveChangesAsync();

        var report = await TeacherReports(db).GetAssignmentsAsync(
            new SpeedReadingTeacherStudentScopeResponse([], [student], 1, [], ReportingTeacherUserId: teacher),
            now.AddDays(-7), now);

        report.DataAvailable.Should().BeTrue();
        report.AssignmentCount.Should().Be(1);
    }

    [Fact]
    public async Task Class_overview_orders_students_by_comprehension_not_mixed_units()
    {
        await using var db = Db();
        var text = Text();
        db.ReadingTexts.Add(text);
        var fastWeak = Guid.NewGuid(); var slowStrong = Guid.NewGuid();
        db.ReadingSessions.AddRange(
            Reading(fastWeak, text.Id, DateTime.UtcNow.AddHours(-1), wpm: 400, comprehension: 50),
            Reading(slowStrong, text.Id, DateTime.UtcNow.AddHours(-1), wpm: 150, comprehension: 90));
        await db.SaveChangesAsync();

        var overview = await TeacherReports(db).GetClassOverviewAsync(
            new SpeedReadingTeacherStudentScopeResponse([], [fastWeak, slowStrong], 2, []), null, null);

        overview.TopPerformers.First().AverageComprehension.Should().Be(90);
        overview.StudentsNeedingSupport.First().AverageComprehension.Should().Be(50);
    }

    [Fact]
    public async Task Institution_reports_only_include_activity_from_the_membership_period()
    {
        await using var db = Db();
        var text = Text();
        db.ReadingTexts.Add(text);
        var institution = Guid.NewGuid(); var student = Guid.NewGuid();
        var joined = DateTime.UtcNow.AddDays(-10);
        db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(institution, student,
            SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), joined));
        db.ReadingSessions.AddRange(
            Reading(student, text.Id, joined.AddDays(-3), wpm: 500, comprehension: 20), // personal activity before joining
            Reading(student, text.Id, joined.AddDays(2), wpm: 200, comprehension: 80));
        await db.SaveChangesAsync();
        var scope = new SpeedReadingTeacherStudentScopeResponse([institution], [], 1, []);

        var overview = await TeacherReports(db).GetClassOverviewAsync(scope, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var time = await TeacherReports(db).GetTimeProgressAsync(scope, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);
        var content = await TeacherReports(db).GetContentAnalysisAsync(scope, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow);

        overview.TotalActivitiesCompleted.Should().Be(1);
        overview.ClassAverageWpm.Should().Be(200);
        time.WeeklyProgressChart.Sum(item => item.Series.Sum(series => series.Value)).Should().Be(1);
        content.ReadingAnalysis.Sum(item => item.TotalReads).Should().Be(1);
    }

    [Fact]
    public async Task Institution_student_reports_start_at_the_membership_date()
    {
        var joined = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc);
        var analytics = new RecordingAnalytics();
        var controller = InstitutionController(new MemberSinceAccess(joined), analytics);

        await controller.GetStudentSummary(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Unspecified), null);
        await controller.GetStudentActivity(Guid.NewGuid(), Guid.NewGuid(), null, null);
        await controller.GetStudentSeries(Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Unspecified), null);

        analytics.From[0].Should().Be(joined);
        analytics.From[1].Should().Be(joined);
        analytics.From[2].Should().Be(new DateTime(2026, 10, 25, 0, 0, 0, DateTimeKind.Unspecified));
    }

    [Fact]
    public void The_unused_client_supplied_report_export_endpoint_is_removed()
    {
        var controller = typeof(OwnedSpeedReadingDbContext).Assembly.GetReferencedAssemblies()
            .Select(Assembly.Load).Concat([typeof(InstitutionAnalyticsController).Assembly])
            .SelectMany(assembly => assembly.GetTypes())
            .FirstOrDefault(type => type.Name == "ReportsController");
        if (controller is null) return;
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Should().NotContain(name => name.Contains("Export", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Report_calendar_reads_date_only_values_as_istanbul_days()
    {
        var day = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Unspecified);
        var (start, end) = SpeedReadingReportCalendar.ResolveRange(day, day, DateTime.UtcNow, 30);
        start.Should().Be(new DateTime(2026, 10, 29, 21, 0, 0, DateTimeKind.Utc));
        end.Should().Be(new DateTime(2026, 10, 30, 21, 0, 0, DateTimeKind.Utc).AddTicks(-1));
        var instant = new DateTime(2026, 10, 30, 8, 15, 0, DateTimeKind.Utc);
        SpeedReadingReportCalendar.ResolveRange(instant, instant.AddHours(1), DateTime.UtcNow, 30)
            .Should().Be((instant, instant.AddHours(1)));
    }

    private static OwnedSpeedReadingDbContext Db() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ProgramTemplate Template(int totalDays) =>
        ProgramTemplate.Import(Guid.NewGuid(), "Program", "", Guid.NewGuid(), 0, 100, "{}", 1, 1, 5, 4, totalDays, true, 1, 1, null, false,
            DateTime.UtcNow, null, null, null);

    private static ReadingText Text() => ReadingText.Import(Guid.NewGuid(), "Metin", "Metin içeriği burada.", "tr", null, 120, "Genel", 1,
        null, true, null, 1, 10, 70m, 3, 60, DateTime.UtcNow, null, null, null);

    private static ReadingSession Reading(Guid user, Guid text, DateTime at, int wpm = 200, decimal comprehension = 80) =>
        ReadingSession.Import(Guid.NewGuid(), user, text, 60, wpm, 4, 5, comprehension, 0, at, at, null, null, null);

    private static ExerciseSessionResult Result(Guid user, Guid exercise, decimal score, DateTime at, bool measured) =>
        ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), user, exercise, null, 0, 60, 0, 0, 0, score, at, isMeasured: measured);

    private static DailyExerciseLog Log(Guid user, Guid progress, int week, int day, DateTime at, Guid? session = null, Guid? exercise = null) =>
        DailyExerciseLog.Import(Guid.NewGuid(), user, progress, exercise ?? Guid.NewGuid(), Guid.NewGuid(), day, week, 1, at, 60, 90, true,
            "{}", 1, false, "web", 9, 1, 10, null, 0, (int)at.DayOfWeek, at.TimeOfDay, 0, 0, 0, 0, 0, 0, false, 0, 1, 0, 0, 0, 0, at,
            null, null, null, sessionId: session);

    private static T Create<T>(string name, params object[] args) => (T)Activator.CreateInstance(
        Infrastructure.GetType($"SpeedReading.Infrastructure.Persistence.{name}", throwOnError: true)!,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args, null)!;

    private static ILegacySpeedReadingAnalytics Analytics(OwnedSpeedReadingDbContext db) =>
        Create<ILegacySpeedReadingAnalytics>("OwnedSpeedReadingAnalytics", db, new MemoryCache(new MemoryCacheOptions()));

    private static ILegacySpeedReadingAdminAnalytics Admin(OwnedSpeedReadingDbContext db, ISpeedReadingInstitutionDirectory? directory = null) =>
        Create<ILegacySpeedReadingAdminAnalytics>("OwnedSpeedReadingAdminAnalytics", db,
            directory ?? new InstitutionDirectory(new SpeedReadingInstitutionScopeResponse([])), new Users());

    private static ILegacySpeedReadingTeacherReports TeacherReports(OwnedSpeedReadingDbContext db) =>
        Create<ILegacySpeedReadingTeacherReports>("OwnedSpeedReadingTeacherReports", db, new Users());

    private static ILegacySpeedReadingProgress Progress(OwnedSpeedReadingDbContext db) =>
        Create<ILegacySpeedReadingProgress>("OwnedSpeedReadingProgress", db);

    private static ILegacySpeedReadingPrograms Programs(OwnedSpeedReadingDbContext db)
    {
        var type = Infrastructure.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingPrograms", throwOnError: true)!;
        var parameters = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single().GetParameters();
        var args = parameters.Select(parameter => parameter.ParameterType == typeof(OwnedSpeedReadingDbContext) ? db
            : parameter.ParameterType == typeof(ISpeedReadingUserDirectory) ? new Users()
            : parameter.ParameterType.IsInterface ? DispatchProxyFactory.Create(parameter.ParameterType) : null).ToArray();
        return (ILegacySpeedReadingPrograms)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, args, null)!;
    }

    private static InstitutionAnalyticsController InstitutionController(ISpeedReadingTeacherAccess access, ILegacySpeedReadingAnalytics analytics)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "InstitutionAdmin")], "Test"));
        return new InstitutionAnalyticsController(TeacherReports(Db()), analytics, access)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
    }

    private sealed class InstitutionDirectory(SpeedReadingInstitutionScopeResponse response) : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(response);
    }

    private sealed class Users : ISpeedReadingUserDirectory
    {
        public Task<SpeedReadingUserDirectoryResponse> GetUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingUserDirectoryResponse([]));
        public Task<IReadOnlyList<Guid>> GetAudienceUserIdsAsync(string? role, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
    }

    private sealed class MemberSinceAccess(DateTime joined) : ISpeedReadingTeacherAccess
    {
        public Task<IReadOnlySet<Guid>> GetReadableStudentIdsAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentUserIds,
            Guid? targetTeacherUserId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        public Task<bool> CanReadStudentAsync(Guid viewerUserId, Guid studentUserId, Guid? targetTeacherUserId = null,
            bool isSystemAdmin = false, bool isInstitutionAdmin = false, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<SpeedReadingTeacherStudentScopeResponse?> GetStudentScopeAsync(Guid viewerUserId, Guid? targetTeacherUserId = null,
            bool isSystemAdmin = false, bool isInstitutionAdmin = false, CancellationToken cancellationToken = default) =>
            Task.FromResult<SpeedReadingTeacherStudentScopeResponse?>(null);
        public Task<SpeedReadingTeacherStudentScopeResponse?> GetInstitutionStudentScopeAsync(Guid viewerUserId, Guid institutionId,
            bool isSystemAdmin = false, CancellationToken cancellationToken = default) => Task.FromResult<SpeedReadingTeacherStudentScopeResponse?>(null);
        public Task<bool> CanReadInstitutionStudentAsync(Guid viewerUserId, Guid institutionId, Guid studentUserId,
            bool isSystemAdmin = false, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<DateTime?> GetInstitutionMemberSinceAsync(Guid institutionId, Guid studentUserId,
            CancellationToken cancellationToken = default) => Task.FromResult<DateTime?>(joined);
    }

    private sealed class RecordingAnalytics : ILegacySpeedReadingAnalytics
    {
        public List<DateTime?> From { get; } = [];
        public Task<StudentAnalyticsSummary> GetStudentSummaryAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        { From.Add(dateFrom); return Task.FromResult<StudentAnalyticsSummary>(null!); }
        public Task<StudentReadingSpeedAnalytics> GetStudentReadingSpeedAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        { From.Add(dateFrom); return Task.FromResult<StudentReadingSpeedAnalytics>(null!); }
        public Task<StudentReadingSpeedAnalytics> GetStudentReadingSpeedAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, Guid? institutionId, CancellationToken cancellationToken)
        { From.Add(dateFrom); return Task.FromResult<StudentReadingSpeedAnalytics>(null!); }
        public Task<StudentComprehensionAnalytics> GetStudentComprehensionAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        { From.Add(dateFrom); return Task.FromResult<StudentComprehensionAnalytics>(null!); }
        public Task<StudentComprehensionAnalytics> GetStudentComprehensionAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, Guid? institutionId, CancellationToken cancellationToken)
        { From.Add(dateFrom); return Task.FromResult<StudentComprehensionAnalytics>(null!); }
        public Task<StudentSeriesAnalytics> GetStudentSeriesAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        { From.Add(dateFrom); return Task.FromResult<StudentSeriesAnalytics>(null!); }
        public Task<StudentActivityAnalytics> GetStudentActivityAsync(Guid userId, DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken = default)
        { From.Add(dateFrom); return Task.FromResult<StudentActivityAnalytics>(null!); }
    }
}

internal class DispatchProxyFactory : DispatchProxy
{
    public static object Create(Type interfaceType) =>
        typeof(DispatchProxy).GetMethod(nameof(DispatchProxy.Create), 2, Type.EmptyTypes)!
            .MakeGenericMethod(interfaceType, typeof(DispatchProxyFactory)).Invoke(null, null)!;

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
        throw new NotSupportedException($"{targetMethod?.Name} is not used by this report test.");
}
