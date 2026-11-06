using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

// Paket C (#3): migration 20261113090000_StudentOnboarding. Students who finished the old setup (an exam purpose and complete
// education) are marked as having completed the first-login wizard; everyone else starts or resumes it. The model has no
// pending changes, and Down / Up round-trip.
public sealed class StudentOnboardingMigrationPostgresTests
{
    private const string Previous = "20261112090000_ScoreModelsAllExams", Current = "20261113090000_StudentOnboarding";

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExistingStudentsWithASetUpExamSkipTheWizard()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create(Previous);
        Guid done = Guid.NewGuid(), graduate = Guid.NewGuid(), noContext = Guid.NewGuid(), retired = Guid.NewGuid(), noGrade = Guid.NewGuid();
        await Profile(f.Db, done, 1, 8); await Context(f.Db, done, 1);
        await Profile(f.Db, graduate, 2, null); await Context(f.Db, graduate, 6);
        await Profile(f.Db, noContext, 1, 11);
        await Profile(f.Db, retired, 1, 9); await Context(f.Db, retired, 0);
        await Profile(f.Db, noGrade, 0, null); await Context(f.Db, noGrade, 2);

        await f.Db.Database.MigrateAsync();
        Assert.False(f.Db.Database.HasPendingModelChanges());
        Assert.Equal(Current, (await f.Db.Database.GetAppliedMigrationsAsync()).Last());
        var rows = await f.Db.CoachingStudentProfiles.AsNoTracking().ToDictionaryAsync(x => x.UserId);
        foreach (var id in new[] { done, graduate })
        {
            Assert.NotNull(rows[id].OnboardingCompletedAt);
            Assert.Equal(7, rows[id].NextOnboardingStep);
            Assert.All(Enumerable.Range(1, 7), step => Assert.True(rows[id].HasOnboardingStep(step)));
        }
        foreach (var id in new[] { noContext, retired, noGrade })
            Assert.Equal((null, 0, 1), (rows[id].OnboardingCompletedAt, rows[id].OnboardingSteps, rows[id].NextOnboardingStep));

        // The new columns are checked and stored.
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == noContext);
        profile.SetHome("06", "Ankara", "0601", "Çankaya"); profile.SetDiplomaGrade(87.25m, true); profile.RecordOnboardingStep(1);
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var stored = await f.Db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.UserId == noContext);
        Assert.Equal(("Ankara", "Çankaya", (decimal?)87.25m, true, 2), (stored.HomeCity, stored.HomeDistrict, stored.DiplomaGrade, stored.DiplomaGradeEstimated, stored.NextOnboardingStep));
        await Assert.ThrowsAnyAsync<Exception>(() => f.Db.Database.ExecuteSqlRawAsync($"UPDATE coaching.student_profiles SET \"DiplomaGrade\" = 40 WHERE \"UserId\" = '{noContext}'"));

        await f.Db.GetService<IMigrator>().MigrateAsync(Previous);
        Assert.Equal(Previous, (await f.Db.Database.GetAppliedMigrationsAsync()).Last());
        await f.Db.Database.MigrateAsync();
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }

    private static Task Profile(DbContext db, Guid user, int status, int? grade) => db.Database.ExecuteSqlRawAsync(
        "INSERT INTO coaching.student_profiles (\"Id\",\"UserId\",\"EducationStatus\",\"GradeLevel\",\"ShareProgressWithTeachers\",\"IsActive\",\"CreatedAt\",\"Version\") " +
        $"VALUES ({{0}},{{1}},{{2}},{(grade is null ? "NULL" : grade.ToString())},true,true,now(),1)", Guid.NewGuid(), user, status);

    private static Task Context(DbContext db, Guid user, int purpose) => db.Database.ExecuteSqlRawAsync(
        "INSERT INTO coaching.student_learning_contexts (\"Id\",\"StudentUserId\",\"AcademicYearStart\",\"Purpose\",\"EducationStatus\",\"LastCompletedStep\",\"SetupVersion\",\"StartingStatusReviewed\",\"CreatedAt\",\"Version\") " +
        "VALUES ({0},{1},2026,{2},1,1,1,false,now(),1)", Guid.NewGuid(), user, purpose);
}
