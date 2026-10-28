using System.Data.Common;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Coaching.Curriculum.IntegrationTests;

// The teacher tracking page runs on a small shared server. Its cost must follow the number of
// students, not the number of keyset batches over their review history, and Identity must not be
// asked once per student while a database snapshot is held open.
public sealed class TeacherTrackingScalePostgresTests
{
    private static readonly DateOnly Week = new(2026, 10, 12);
    private const int Students = 40;
    private const int Weeks = 52;

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task LongReviewedHistoryIsComparedPerStudentAndIdentityIsAskedOutsideTheSnapshot()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var (teacher, _) = await SeedReviewedHistory(pg);
        var counter = new CommandCounter();
        await using var reader = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(counter).Options);
        var accounts = new CountingAccounts(teacher, () => reader.Database.CurrentTransaction is not null);
        pg.Data.User.UserId = teacher; pg.Data.User.Roles = ["Teacher"];
        var api = new CoachingTeacherTrackingController(reader, new CoachingAccessPolicy(pg.Data.User),
            new OwnedCoachingDirectory(reader, accounts, pg.Data.User), accounts, new WeeklyFollowUpPostgresTests.Clock());

        var page = Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(
            await api.Get(new(2026, 10, 19), new(2026, 10, 20), "weekly-review", null, 1, 25)).Value);

        // Every seeded review carries a stale fingerprint, so every week is still compared and reported.
        Assert.Equal(Students * Weeks, page.AwaitingWeeklyReviewCount);
        Assert.All(page.Items, item => Assert.Equal("ResultsChanged", item.ReviewReason));
        // One result read per student for each 26-week chunk (a handful of queries each), plus fixed overhead.
        // Reading per keyset batch instead repeats the per-student reads for every 256 reflections.
        Assert.True(counter.Commands <= Students * 2 * 8 + 60, $"Tracking page executed {counter.Commands} commands.");
        Assert.True(accounts.InstitutionChecks <= 1, $"Institution activity was asked {accounts.InstitutionChecks} times.");
        Assert.Equal(0, accounts.CallsInsideTransaction);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StudentReadAuthorizationAsksIdentityOncePerInstitutionAndKeepsDecisions()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var (teacher, students) = await SeedReviewedHistory(pg, weeks: 0);
        var hidden = students[0];
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.student_profiles SET "ShareProgressWithTeachers" = false WHERE "UserId" = {hidden}
            """);
        var accounts = new CountingAccounts(teacher, () => false);
        var directory = new OwnedCoachingDirectory(pg.Db, accounts, pg.Data.User);

        var allowed = await directory.AuthorizeStudentReadAsync(teacher, students, default);

        Assert.Equal(students.Skip(1).OrderBy(x => x), allowed.OrderBy(x => x));
        Assert.Equal(1, accounts.InstitutionChecks);
    }

    private static async Task<(Guid Teacher, Guid[] Students)> SeedReviewedHistory(CurriculumPostgresTests.Pg pg, int weeks = Weeks)
    {
        var teacher = Guid.NewGuid(); var institution = Guid.NewGuid();
        var students = Enumerable.Range(0, Students).Select(_ => Guid.NewGuid()).ToArray();
        pg.Db.AddRange(CoachingTeacherProfile.Create(teacher),
            CoachingInstitutionMembership.Create(institution, teacher, CoachingMemberRole.Teacher, teacher));
        foreach (var student in students)
            pg.Db.AddRange(CoachingStudentProfile.Create(student),
                CoachingInstitutionMembership.Create(institution, student, CoachingMemberRole.Student, teacher),
                CoachingTeacherStudentAssignment.Create(teacher, student, institution, null, teacher));
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        if (weeks == 0) return (teacher, students);
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.weekly_reflections
            ("Id","StudentId","WeekStart","Successes","Obstacles","NextFocus","Rating","Version","CreatedAt","ReflectionHistoryJson")
            SELECT gen_random_uuid(), s, {Week} - 7*i, 'Başarı', '', 'Devam', 3, 3, {DateTime.UtcNow}, '[]'::jsonb
            FROM unnest({students}) s, generate_series(0, {weeks - 1}) g(i)
            """);
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.weekly_reflection_reviews
            ("Id","ReflectionId","StudentId","TeacherId","ReflectionVersion","Note","ReflectionSnapshotJson","EvidenceFingerprint","EvidenceSnapshotJson","Version","CreatedAt")
            SELECT gen_random_uuid(), "Id", "StudentId", {teacher}, "Version", 'Geçmiş inceleme', jsonb_build_object(), {new string('a', 64)},
                jsonb_build_object(), 0, {DateTime.UtcNow}
            FROM coaching.weekly_reflections
            """);
        return (teacher, students);
    }

    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Commands { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default) { Commands++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken ct = default) { Commands++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default) { Commands++; return ValueTask.FromResult(result); }
    }

    private sealed class CountingAccounts(Guid teacher, Func<bool> insideTransaction) : ICoachingAccountDirectory
    {
        public int InstitutionChecks { get; private set; }
        public int CallsInsideTransaction { get; private set; }
        private void Track() { if (insideTransaction()) CallsInsideTransaction++; }
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct)
        {
            Track();
            return Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id, "Test", "User",
                "test@example.test", true, true, [id == teacher ? "Teacher" : "Student"], null)).ToArray());
        }
        public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) { Track(); InstitutionChecks++; return Task.FromResult(true); }
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) => Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct)
            => Task.FromResult<CoachingAgreementRepresentativeAuthorization?>(null);
    }
}
