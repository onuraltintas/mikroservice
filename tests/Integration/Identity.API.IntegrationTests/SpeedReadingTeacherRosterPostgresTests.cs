using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class SpeedReadingTeacherRosterPostgresTests(PostgresFixture postgres)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Empty_roster_executes_with_and_without_institution_scope(bool institutionScope)
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var service = new OwnedSpeedReadingTeacherStudentAssignments(db, new EligibleMembers());
            var result = await service.GetTeacherRosterAsync(Guid.NewGuid(), institutionScope ? [Guid.NewGuid()] : [], 1, 25);
            Assert.Equal(0, result.TotalCount);
            Assert.Empty(result.Items);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task Roster_preserves_standalone_and_active_institution_scope_and_pagination()
    {
        await using var db = CreateContext();
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var teacher = Guid.NewGuid(); var institution = Guid.NewGuid(); var otherInstitution = Guid.NewGuid();
            var standalone = Guid.NewGuid(); var scoped = Guid.NewGuid(); var outside = Guid.NewGuid(); var inactive = Guid.NewGuid();
            var now = DateTime.UtcNow;
            void Member(Guid id, Guid user, SpeedReadingInstitutionMemberRole role, bool active = true)
            {
                var member = SpeedReadingInstitutionMembership.Create(id, user, role, teacher, now);
                if (!active) member.SetActive(false, teacher, now);
                db.InstitutionMemberships.Add(member);
            }
            Member(institution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
            Member(otherInstitution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
            Member(institution, scoped, SpeedReadingInstitutionMemberRole.Student);
            Member(otherInstitution, outside, SpeedReadingInstitutionMemberRole.Student);
            Member(institution, inactive, SpeedReadingInstitutionMemberRole.Student, false);
            db.TeacherStudentAssignments.AddRange(
                SpeedReadingTeacherStudentAssignment.Create(null, teacher, standalone, teacher, now),
                SpeedReadingTeacherStudentAssignment.Create(institution, teacher, scoped, teacher, now.AddSeconds(1)),
                SpeedReadingTeacherStudentAssignment.Create(otherInstitution, teacher, outside, teacher, now),
                SpeedReadingTeacherStudentAssignment.Create(institution, teacher, inactive, teacher, now));
            await db.SaveChangesAsync();
            var service = new OwnedSpeedReadingTeacherStudentAssignments(db, new EligibleMembers());
            var roster = await service.GetTeacherRosterAsync(teacher, [institution], 1, 25);
            Assert.Equal(2, roster.TotalCount);
            Assert.Equal(new[] { standalone, scoped }, roster.Items.Select(x => x.StudentUserId));
            Assert.Null(roster.Items[0].InstitutionId);
            var secondPage = await service.GetTeacherRosterAsync(teacher, [institution], 2, 1);
            Assert.Equal(2, secondPage.TotalCount);
            Assert.Equal(scoped, Assert.Single(secondPage.Items).StudentUserId);
            var standaloneOnly = await service.GetTeacherRosterAsync(teacher, [], 1, 25);
            Assert.Equal(standalone, Assert.Single(standaloneOnly.Items).StudentUserId);
            var filtered = await service.GetTeacherRosterAsync(teacher, [institution], 1, 1, studentUserId: scoped);
            Assert.Equal(scoped, Assert.Single(filtered.Items).StudentUserId);
            var institutionOnly = await service.GetStudentsAsync(institution, teacher, 1, 25);
            Assert.Equal(scoped, Assert.Single(institutionOnly.Items).StudentUserId);
            Assert.Empty((await service.GetTeacherRosterAsync(Guid.NewGuid(), [institution], 1, 25)).Items);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private OwnedSpeedReadingDbContext CreateContext() => new(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
    private sealed class EligibleMembers : ISpeedReadingInstitutionMemberEligibility
    {
        public Task<bool> IsEligibleAsync(Guid userId, SpeedReadingInstitutionMemberRole role, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }
}
