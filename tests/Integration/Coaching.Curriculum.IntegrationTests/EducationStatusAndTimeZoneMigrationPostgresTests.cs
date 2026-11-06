using Coaching.Domain.Profiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

// E3 (user decisions 06.11.2026), migration 20261115090000_EducationStatusesAndIstanbulTime.
// #14: "graduate" (2) keeps its number and now means lise mezunu; a graduate preparing for AGS (purpose 5) becomes a university
// graduate (4), the only status AGS fits. Students keep their grade. The check constraint allows the statuses 0–4 and a grade only
// for students. Down refuses while a university status (3, 4) exists.
// #34: every stored zone (study hours, weekly draft, plan snapshot) becomes Europe/Istanbul and is held there by check constraints.
// Planned dates and minutes stay as they are (wall clock).
public sealed class EducationStatusAndTimeZoneMigrationPostgresTests
{
    private const string Previous = "20261114090000_CorrectHomeLocationNames", Current = "20261115090000_EducationStatusesAndIstanbulTime";

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task GraduatesPreparingForAgsBecomeUniversityGraduatesAndOthersKeepTheirEducation()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(Previous);
        Guid ags = Guid.NewGuid(), yds = Guid.NewGuid(), noContext = Guid.NewGuid(), student = Guid.NewGuid(), unknown = Guid.NewGuid(), agsStudent = Guid.NewGuid();
        await Profile(f.Db, ags, 2, null); await Context(f.Db, ags, 5, 2, null);
        await Profile(f.Db, yds, 2, null); await Context(f.Db, yds, 6, 2, null);
        await Profile(f.Db, noContext, 2, null);
        await Profile(f.Db, student, 1, 11); await Context(f.Db, student, 2, 1, 11);
        await Profile(f.Db, unknown, 0, null);
        await Profile(f.Db, agsStudent, 1, 12); await Context(f.Db, agsStudent, 5, 1, 12);

        await f.Db.Database.MigrateAsync();
        Assert.False(f.Db.Database.HasPendingModelChanges());
        Assert.Contains(Current, await f.Db.Database.GetAppliedMigrationsAsync());
        var profiles = await f.Db.CoachingStudentProfiles.AsNoTracking().ToDictionaryAsync(x => x.UserId, x => (x.EducationStatus, x.GradeLevel));
        Assert.Equal((CoachingEducationStatus.UniversityGraduate, (int?)null), profiles[ags]);
        Assert.Equal((CoachingEducationStatus.HighSchoolGraduate, (int?)null), profiles[yds]);
        Assert.Equal((CoachingEducationStatus.HighSchoolGraduate, (int?)null), profiles[noContext]);
        Assert.Equal((CoachingEducationStatus.Student, (int?)11), profiles[student]);
        Assert.Equal((CoachingEducationStatus.Unknown, (int?)null), profiles[unknown]);
        Assert.Equal((CoachingEducationStatus.Student, (int?)12), profiles[agsStudent]);
        // The study settings keep the profile's copy in step.
        var contexts = await f.Db.StudentLearningContexts.AsNoTracking().ToDictionaryAsync(x => x.StudentUserId, x => x.EducationStatus);
        Assert.Equal(CoachingEducationStatus.UniversityGraduate, contexts[ags]);
        Assert.Equal(CoachingEducationStatus.HighSchoolGraduate, contexts[yds]);
        Assert.Equal(CoachingEducationStatus.Student, contexts[agsStudent]);

        // The new statuses are stored; a grade belongs to a student only, and nothing past 4 is allowed.
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync($"UPDATE coaching.student_profiles SET \"EducationStatus\" = 5 WHERE \"UserId\" = '{unknown}'"));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync($"UPDATE coaching.student_profiles SET \"GradeLevel\" = 10 WHERE \"UserId\" = '{ags}'"));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync($"UPDATE coaching.student_profiles SET \"EducationStatus\" = 3, \"GraduationYear\" = 2020 WHERE \"UserId\" = '{unknown}'"));
        await f.Db.Database.ExecuteSqlRawAsync($"UPDATE coaching.student_profiles SET \"GraduationYear\" = 2024 WHERE \"UserId\" = '{ags}'");
        await f.Db.Database.ExecuteSqlRawAsync($"UPDATE coaching.student_profiles SET \"EducationStatus\" = 3 WHERE \"UserId\" = '{unknown}'");

        // Down refuses while a university status exists (the old model has no place for it) and changes nothing.
        var refused = await Assert.ThrowsAnyAsync<Exception>(() => f.Db.GetService<IMigrator>().MigrateAsync(Previous));
        Assert.Contains("EducationStatusesAndIstanbulTime down refused: 2 student profile(s)", refused.ToString());
        Assert.Contains(Current, await f.Db.Database.GetAppliedMigrationsAsync());
        Assert.Equal(CoachingEducationStatus.UniversityStudent, (await f.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == unknown)).EducationStatus);
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.student_profiles SET "EducationStatus" = 2 WHERE "EducationStatus" IN (3, 4)""");
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.student_learning_contexts SET "EducationStatus" = 2 WHERE "EducationStatus" IN (3, 4)""");
        await f.Db.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(Previous, (await f.Db.Database.GetAppliedMigrationsAsync()).Last());
        await f.Db.Database.MigrateAsync();
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EveryStoredZoneBecomesIstanbulAndStaysThere()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(Previous);
        Guid london = Guid.NewGuid(), istanbul = Guid.NewGuid(), plan = Guid.NewGuid(), oldPlan = Guid.NewGuid();
        var hoursLondon = Coaching.Domain.Entities.StudyAvailability.Create(london, "Europe/Istanbul");
        hoursLondon.ReplaceWindows([new(DayOfWeek.Monday, 1080, 1140)]);
        f.Db.AddRange(hoursLondon, Coaching.Domain.Entities.StudyAvailability.Create(istanbul, "Europe/Istanbul"), Coaching.Domain.Entities.WeeklyStudyDraft.Create(london));
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        // Zones chosen on the old settings page (written as they were stored).
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_availability SET "TimeZoneId" = 'Europe/London' WHERE "StudentId" = {0}""", london);
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_weekly_drafts SET "TimeZoneId" = 'America/New_York' WHERE "StudentId" = {0}""", london);
        foreach (var (id, owner) in new[] { (plan, london), (oldPlan, istanbul) })   // one draft plan per student
        {
            var revision = Coaching.Domain.Entities.StudyPlanRevision.Create(owner, Guid.NewGuid(), 1, "Plan");
            revision.SetWeeklySource(new(1, null, new(2026, 11, 9), new(2026, 11, 15), false, "f", null, null));
            f.Db.Add(revision); await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
            await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_plan_revisions SET "Id" = {0} WHERE "Id" = {1}""", id, revision.Id);
        }
        // A plan made in another zone, and an older snapshot from before the zone was recorded.
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_plan_revisions SET "WeeklySource" = jsonb_set("WeeklySource", '{{TimeZoneId}}', '"Asia/Tokyo"') WHERE "Id" = {0}""", plan);
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_plan_revisions SET "WeeklySource" = "WeeklySource" - 'TimeZoneId' WHERE "Id" = {0}""", oldPlan);

        await f.Db.Database.MigrateAsync();
        Assert.False(f.Db.Database.HasPendingModelChanges());
        Assert.All(await f.Db.StudyAvailability.AsNoTracking().Select(x => x.TimeZoneId).ToListAsync(), zone => Assert.Equal("Europe/Istanbul", zone));
        var hours = await f.Db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == london);
        Assert.Equal(1080, Assert.Single(hours.Windows).StartMinute);   // wall clock: 18:00 stays 18:00
        Assert.Equal("Europe/Istanbul", (await f.Db.Set<Coaching.Domain.Entities.WeeklyStudyDraft>().AsNoTracking().SingleAsync()).TimeZoneId);
        Assert.Equal("Europe/Istanbul", (await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x => x.Id == plan)).WeeklySource!.TimeZoneId);
        Assert.Equal("Europe/Istanbul", (await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x => x.Id == oldPlan)).WeeklySource!.TimeZoneId);
        // Nothing else can be stored.
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_availability SET "TimeZoneId" = 'UTC'"""));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_weekly_drafts SET "TimeZoneId" = 'UTC'"""));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_plan_revisions SET "WeeklySource" = jsonb_set("WeeklySource", '{TimeZoneId}', '"UTC"')"""));
        // Down only lifts the constraints and keeps the zones in Istanbul; a zone written while rolled back is moved to Istanbul again by Up.
        await f.Db.GetService<IMigrator>().MigrateAsync(Previous);
        await f.Db.Database.ExecuteSqlRawAsync("""UPDATE coaching.study_availability SET "TimeZoneId" = 'UTC' WHERE "StudentId" = {0}""", istanbul);
        await f.Db.Database.MigrateAsync();
        Assert.Equal("Europe/Istanbul", (await f.Db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == istanbul)).TimeZoneId);
    }

    private static Task Profile(DbContext db, Guid user, int status, int? grade) => db.Database.ExecuteSqlRawAsync(
        "INSERT INTO coaching.student_profiles (\"Id\",\"UserId\",\"EducationStatus\",\"GradeLevel\",\"ShareProgressWithTeachers\",\"IsActive\",\"CreatedAt\",\"Version\") " +
        $"VALUES ({{0}},{{1}},{{2}},{(grade is null ? "NULL" : grade.ToString())},true,true,now(),1)", Guid.NewGuid(), user, status);

    private static Task Context(DbContext db, Guid user, int purpose, int status, int? grade) => db.Database.ExecuteSqlRawAsync(
        "INSERT INTO coaching.student_learning_contexts (\"Id\",\"StudentUserId\",\"AcademicYearStart\",\"Purpose\",\"EducationStatus\",\"GradeLevel\",\"LastCompletedStep\",\"SetupVersion\",\"StartingStatusReviewed\",\"CreatedAt\",\"Version\") " +
        $"VALUES ({{0}},{{1}},2026,{{2}},{{3}},{(grade is null ? "NULL" : grade.ToString())},1,1,false,now(),1)", Guid.NewGuid(), user, purpose, status);
}
