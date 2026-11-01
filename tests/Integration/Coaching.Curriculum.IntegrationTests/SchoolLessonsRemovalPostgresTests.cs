using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Curriculum.IntegrationTests;

// KM8 (01.11.2026): content-packs/school-lessons-removal/v1 on the fully migrated Coaching schema (real foreign keys, the
// exam-history triggers and the audit table). The node rehearsal (tests/E2E/school-lessons-removal.postgres.test.mjs) runs
// the same files on the production catalog export.
public sealed class SchoolLessonsRemovalPostgresTests
{
    private static readonly string Pack = Path.Combine(Root(), "content-packs", "school-lessons-removal", "v1");

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "content-packs"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static async Task<NpgsqlConnection> Open(CurriculumPostgresTests.Pg pg)
    {
        var connection = (NpgsqlConnection)pg.Db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        return connection;
    }

    private static async Task<string> Scalar(CurriculumPostgresTests.Pg pg, string sql)
    {
        await using var command = new NpgsqlCommand(sql, await Open(pg));
        return Convert.ToString(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }

    private static async Task<List<string>> Rows(CurriculumPostgresTests.Pg pg, string sql)
    {
        var rows = new List<string>();
        await using var command = new NpgsqlCommand(sql, await Open(pg));
        await using var reader = await command.ExecuteReaderAsync();
        do { while (await reader.ReadAsync()) rows.Add(reader.GetString(0)); } while (await reader.NextResultAsync());
        return rows;
    }

    private static async Task<List<string>> Run(CurriculumPostgresTests.Pg pg, string sql)
    {
        var connection = await Open(pg);
        var notices = new List<string>();
        void Collect(object? sender, NpgsqlNoticeEventArgs e) => notices.Add(e.Notice.MessageText);
        connection.Notice += Collect;
        try { await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(); }
        catch (PostgresException)
        {
            await using var rollback = new NpgsqlCommand("ROLLBACK", connection); await rollback.ExecuteNonQueryAsync();
            throw;
        }
        finally { connection.Notice -= Collect; }
        return notices;
    }

    private static Task<string> Catalog(CurriculumPostgresTests.Pg pg) => Scalar(pg, """
        SELECT md5(string_agg(x, '|' ORDER BY x)) FROM (
            SELECT l::text x FROM coaching.study_catalog_lessons l UNION ALL SELECT u::text FROM coaching.study_catalog_units u
            UNION ALL SELECT t::text FROM coaching.study_catalog_topics t) rows
        """);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RemovesUnusedSchoolLessonsDeactivatesUsedOnesAndRestores()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var list = File.ReadAllLines(Path.Combine(Pack, "lessons.csv")).Skip(1).Select(x => (Id: Guid.Parse(x[..36]), Name: x[37..])).ToArray();
        Assert.Equal(141, list.Length);
        // The 141 exam-less lessons, each with one unit and a parent + child topic; one TYT lesson that must stay.
        var catalog = CurriculumVersion.Create("school-2026", "Okul programı", "v1", 2026, 2026, "Test kaynağı", null);
        pg.Db.Add(catalog);
        var topics = new Dictionary<Guid, (Guid Unit, Guid Parent, Guid Child)>();
        foreach (var (lesson, index) in list.Select((x, i) => (x, i)))
        {
            await Run(pg, $"""
                INSERT INTO coaching.study_catalog_lessons ("Id","Source","SourceId","Name","GradeNumber","ExamCode","IsActive")
                VALUES ('{lesson.Id}', 'approved-catalog', 'lesson-{index}', '{lesson.Name.Replace("'", "''")}', NULL, NULL, true);
                """);
            var unit = StudyCatalogUnit.Create("approved-catalog", $"unit-{index}", lesson.Id, "Ünite", 1); unit.SetActive(true);
            var parent = StudyCatalogTopic.Create("approved-catalog", $"upper:{index}", lesson.Id, unit.Id, "Üst konu", null, 1); parent.SetActive(true);
            var child = StudyCatalogTopic.Create("approved-catalog", $"topic:{index}", lesson.Id, unit.Id, "Konu", parent.Id, 1, 30); child.SetActive(true);
            pg.Db.AddRange(unit, parent, child); topics[lesson.Id] = (unit.Id, parent.Id, child.Id);
        }
        var keep = StudyCatalogLesson.Create("approved-catalog", "tyt-mat", "TYT Matematik", null, "TYT"); keep.SetActive(true);
        var keepUnit = StudyCatalogUnit.Create("approved-catalog", "tyt-unit", keep.Id, "Sayılar", 1); keepUnit.SetActive(true);
        var keepTopic = StudyCatalogTopic.Create("approved-catalog", "tyt-topic", keep.Id, keepUnit.Id, "Temel kavramlar", null, 1, 60); keepTopic.SetActive(true);
        pg.Db.AddRange(keep, keepUnit, keepTopic);
        await pg.Db.SaveChangesAsync();

        // Student data on four of them: a course preference, a declared topic state, plan tasks (topic and weekly lesson) and a weekly draft.
        var student = Guid.NewGuid();
        var context = StudentLearningContext.Create(student, 2026, LearningPurpose.School, null, null, null);
        context.ReplaceSelections([StudentCoursePreference.Create(context.Id, list[0].Id, 1, null)],
            [StudentTopicState.Create(context.Id, topics[list[1].Id].Child, DeclaredTopicStatus.Studied, student)], true);
        var revision = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Okul planım"); revision.Activate();
        var topicTask = StudyPlanTask.Create(revision, new(2026, 10, 19), "Konu", 30, topics[list[2].Id].Child);
        var lessonTask = StudyPlanTask.Create(revision, new(2026, 10, 20), "Haftalık ders", 30);
        var draft = WeeklyStudyDraft.Create(student);
        draft.Replace("Europe/Istanbul", [new(Guid.NewGuid(), list[3].Id, list[3].Name, null, DayOfWeek.Monday, 900, 60)]);
        pg.Db.AddRange(CoachingStudentProfile.Create(student), context, revision, topicTask, lessonTask, draft);
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        await Run(pg, $"""UPDATE coaching.study_plan_tasks SET "WeeklyLessonId" = '{list[4].Id}', "LessonNameSnapshot" = 'Okul dersi', "PlannedStartMinute" = 900 WHERE "Id" = '{lessonTask.Id}'""");
        var before = await Catalog(pg);

        var check = await Rows(pg, await File.ReadAllTextAsync(Path.Combine(Pack, "check.sql")));
        Assert.Contains(check, x => x.StartsWith("list|ok|141 lessons", StringComparison.Ordinal));
        Assert.Contains("total|lessons delete 136 deactivate 5|units delete 139 deactivate 2|topics delete 278 deactivate 4|student references 5", check);
        Assert.Equal(before, await Catalog(pg));

        var applied = await Run(pg, await File.ReadAllTextAsync(Path.Combine(Pack, "apply.sql")));
        Assert.Contains(applied, x => x.StartsWith("deleted: lessons 136, units 139, topics 278", StringComparison.Ordinal));
        Assert.Contains(applied, x => x.StartsWith("deactivated: lessons 5, units 2, topics 4", StringComparison.Ordinal));
        Assert.Equal("0/5", await Scalar(pg, """SELECT count(*) FILTER (WHERE "IsActive") || '/' || count(*) FROM coaching.study_catalog_lessons WHERE "ExamCode" IS NULL"""));
        Assert.Equal("True", await Scalar(pg, $"""SELECT "IsActive" FROM coaching.study_catalog_lessons WHERE "Id" = '{keep.Id}'"""));
        Assert.Equal("True", await Scalar(pg, $"""SELECT "IsActive" FROM coaching.study_catalog_topics WHERE "Id" = '{keepTopic.Id}'"""));
        Assert.Equal("564", await Scalar(pg, """SELECT count(*) FROM coaching."AdminAuditRecords" WHERE "ChangedFieldsJson"::jsonb ->> 'pack' = 'school-lessons-removal/v1'"""));
        Assert.Contains("school-lessons-removal verify: ok", await Rows(pg, await File.ReadAllTextAsync(Path.Combine(Pack, "verify.sql"))));
        // Student data still points at the deactivated rows.
        Assert.Equal("1", await Scalar(pg, "SELECT count(*) FROM coaching.student_course_preferences"));
        Assert.Equal("1", await Scalar(pg, "SELECT count(*) FROM coaching.student_topic_states"));

        var restored = await Run(pg, await File.ReadAllTextAsync(Path.Combine(Pack, "restore.sql")));
        Assert.Contains(restored, x => x.StartsWith("restored: lessons 136, units 139, topics 278; reactivated: lessons 5, units 2, topics 4", StringComparison.Ordinal));
        Assert.Equal(before, await Catalog(pg));
    }
}
