using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Coaching.Curriculum.IntegrationTests;

// KM1 (01.11.2026): an exam can have several dated sessions (YDS and YÖKDİL twice a year, e-YDS many times). The old single
// study_catalog_exams."ExamDate" becomes the exam's first session; the student may pick the session they aim for.
public sealed class ExamSessionsPostgresTests
{
    private const string Previous = "20261108090000_AssignmentLastSubmittedAt";
    private const string Migration = "20261109090000_ExamSessionsAndNewPurposes";
    private static readonly string Pack = Path.Combine(Root(), "content-packs", "exam-calendar", "2027");

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static async Task<string> Scalar(CurriculumPostgresTests.Pg pg, string sql)
    {
        var connection = (NpgsqlConnection)pg.Db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static async Task<List<string>> Run(CurriculumPostgresTests.Pg pg, string sql)
    {
        var connection = (NpgsqlConnection)pg.Db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        var notices = new List<string>();
        void Collect(object? sender, NpgsqlNoticeEventArgs e) => notices.Add(e.Notice.MessageText);
        connection.Notice += Collect;
        try { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
        finally { connection.Notice -= Collect; }
        return notices;
    }

    private static Task<string> Sessions(CurriculumPostgresTests.Pg pg) => Scalar(pg, """
        SELECT coalesce(string_agg(e."Code" || ':' || s."Label" || ':' || s."Date"::text || ':' || coalesce(s."ApplicationStart"::text, '-')
            || ':' || coalesce(s."ApplicationEnd"::text, '-'), ',' ORDER BY e."Code", s."Date"), '')
        FROM coaching.study_catalog_exam_sessions s JOIN coaching.study_catalog_exams e ON e."Id" = s."ExamId"
        """);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExamDateBecomesTheFirstSessionAndRollsBack()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(Previous);
        await Run(pg, """
            UPDATE coaching.study_catalog_exams SET "ExamDate" = DATE '2027-06-20' WHERE "Code" = 'LGS';
            UPDATE coaching.study_catalog_exams SET "ExamDate" = DATE '2026-06-21' WHERE "Code" = 'TYT';
            """);
        var migrator = pg.Db.GetService<IMigrator>();
        await migrator.MigrateAsync(Migration);
        Assert.Equal("LGS:2027:2027-06-20:-:-,TYT:2026:2026-06-21:-:-", await Sessions(pg));
        Assert.Equal("0", await Scalar(pg, "SELECT count(*) FROM information_schema.columns WHERE table_schema = 'coaching' AND table_name = 'study_catalog_exams' AND column_name = 'ExamDate'"));
        Assert.Empty(await pg.Db.Database.GetPendingMigrationsAsync());
        Assert.False(pg.Db.Database.HasPendingModelChanges());

        // The new purposes are allowed by the database; a student on one of them blocks the rollback.
        var student = Guid.NewGuid();
        pg.Db.Add(StudentLearningContext.Create(student, 2026, LearningPurpose.Yokdil, 2027, "YOKDIL_SOS", null));
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(Previous));
        await Run(pg, $"DELETE FROM coaching.student_learning_contexts WHERE \"StudentUserId\" = '{student}'");

        // A second session on the same exam: the rollback keeps the next upcoming date (else the latest one).
        await Run(pg, """
            INSERT INTO coaching.study_catalog_exam_sessions ("Id","ExamId","Date","Label")
            SELECT gen_random_uuid(), "Id", DATE '2099-06-20', '2099' FROM coaching.study_catalog_exams WHERE "Code" = 'LGS';
            """);
        await migrator.MigrateAsync(Previous);
        Assert.Equal("LGS:2027-06-20,TYT:2026-06-21", await Scalar(pg, """
            SELECT string_agg("Code" || ':' || "ExamDate"::text, ',' ORDER BY "Code") FROM coaching.study_catalog_exams WHERE "ExamDate" IS NOT NULL
            """));
        Assert.Equal("", await Scalar(pg, "SELECT coalesce(to_regclass('coaching.study_catalog_exam_sessions')::text, '')"));
        await pg.Db.Database.MigrateAsync();
        Assert.Equal("LGS:2027:2027-06-20:-:-,TYT:2026:2026-06-21:-:-", await Sessions(pg));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CalendarPackAddsThe2027SessionsOnceAndLoadsNewExamsPassive()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var apply = await File.ReadAllTextAsync(Path.Combine(Pack, "apply.sql"));
        var verify = await File.ReadAllTextAsync(Path.Combine(Pack, "verify.sql"));
        var first = await Run(pg, apply);
        Assert.Contains(first, x => x.StartsWith("before: exams 5", StringComparison.Ordinal));
        Assert.Contains(first, x => x.StartsWith("after: exams 9", StringComparison.Ordinal));
        Assert.Equal(
            "AGS:2027:2027-05-08:2027-03-19:2027-03-29,AYT:2027:2027-06-20:2027-02-04:2027-03-02,OABT:2027:2027-05-09:2027-03-19:2027-03-29,"
            + "TYT:2027:2027-06-19:2027-02-04:2027-03-02,YDS:2027/1:2027-03-28:2027-02-03:2027-02-11,YDS:2027/2:2027-11-21:2027-10-05:2027-10-13,"
            + "YDT:2027:2027-06-20:2027-02-04:2027-03-02,YOKDIL:2027/1:2027-02-28:2027-01-12:2027-01-20,YOKDIL:2027/2:2027-07-25:2027-06-08:2027-06-16",
            await Sessions(pg));
        Assert.Equal("AGS:f,LGS:t,OABT:f,TDP:t,TYT:t,YDS:f,YDT:t,YOKDIL:f,AYT:t".Split(',').Order().ToArray(),
            (await Scalar(pg, "SELECT string_agg(\"Code\" || ':' || \"IsActive\", ',') FROM coaching.study_catalog_exams")).Split(',').Order().ToArray());
        var snapshot = await Sessions(pg);
        var second = await Run(pg, apply);
        Assert.Contains(second, x => x.StartsWith("before: exams 9, 2027 sessions 9", StringComparison.Ordinal));
        Assert.Equal(snapshot, await Sessions(pg));
        Assert.Contains(await Run(pg, verify), x => x.StartsWith("ok:", StringComparison.Ordinal));

        // A different session already on a 2027 date stops the pack without changing anything.
        await Run(pg, """
            DELETE FROM coaching.study_catalog_exam_sessions WHERE "Label" = '2027/1' AND "ExamId" = (SELECT "Id" FROM coaching.study_catalog_exams WHERE "Code" = 'YDS');
            INSERT INTO coaching.study_catalog_exam_sessions ("Id","ExamId","Date","Label")
            SELECT gen_random_uuid(), "Id", DATE '2027-03-28', 'Bahar' FROM coaching.study_catalog_exams WHERE "Code" = 'YDS';
            """);
        var before = await Sessions(pg);
        await Assert.ThrowsAsync<PostgresException>(() => Run(pg, apply));
        Assert.Equal(before, await Sessions(pg));
        await Assert.ThrowsAsync<PostgresException>(() => Run(pg, verify));
    }

    private sealed class NoReaders : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId, IReadOnlyCollection<Guid> studentIds, CancellationToken ct) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TodayCountsDownToTheChosenSessionThenTheNextCatalogSession()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var now = new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero); var clock = new Clock(now);
        var student = Guid.NewGuid(); pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        var yds = StudyCatalogExam.Create("YDS", "YDS");
        yds.ReplaceSessions([new(null, new(2027, 3, 28), "2027/1", null, null), new(null, new(2027, 11, 21), "2027/2", null, null)]);
        var lesson = StudyCatalogLesson.Create("test", "yds-en", "YDS İngilizce", null, "YDS"); lesson.SetActive(true);
        pg.Db.AddRange(yds, CoachingStudentProfile.Create(student)); await pg.Db.SaveChangesAsync();
        pg.Db.Add(lesson); await pg.Db.SaveChangesAsync();
        var weekly = WeeklyStudyDraft.Create(student); weekly.Replace("Europe/Istanbul", [new(Guid.NewGuid(), lesson.Id, lesson.Name, "YDS", DayOfWeek.Monday, 1080, 30)]);
        var context = StudentLearningContext.Create(student, 2026, LearningPurpose.Yds, 2027, null, null);
        pg.Db.AddRange(weekly, context); await pg.Db.SaveChangesAsync();
        var api = new StudentTodayController(pg.Db, new CoachingAccessPolicy(pg.Data.User),
            new CoachingStudentTodayService(pg.Db, new CoachingMotivationService(pg.Db, new NoReaders(), clock), clock), clock);
        async Task<StudentTodayExam?> Exam() { pg.Db.ChangeTracker.Clear(); return Assert.IsType<StudentTodayView>(Assert.IsType<OkObjectResult>(await api.Get()).Value).Exam; }

        Assert.Equal(new StudentTodayExam("YDS", new(2027, 3, 28), 146, "Catalog"), await Exam());
        context.SetTargetExamSession(yds.Sessions[1].Id); pg.Db.Update(context); await pg.Db.SaveChangesAsync();
        Assert.Equal(new StudentTodayExam("YDS · 2027/2", new(2027, 11, 21), 384, "Session"), await Exam());
        context.SetTargetExamDate(new(2027, 5, 1), new(2026, 11, 2)); pg.Db.Update(context); await pg.Db.SaveChangesAsync();
        Assert.Equal(new StudentTodayExam("Sınavın", new(2027, 5, 1), 180, "Student"), await Exam());
    }
}
