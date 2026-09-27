using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingInstitutionMembershipTests
{
    [Theory]
    [InlineData(SpeedReadingInstitutionMemberRole.Student)]
    [InlineData(SpeedReadingInstitutionMemberRole.Teacher)]
    public void Create_keeps_institution_membership_inside_speed_reading_scope(
        SpeedReadingInstitutionMemberRole role)
    {
        var userId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();

        var membership = SpeedReadingInstitutionMembership.Create(
            institutionId,
            userId,
            role,
            userId,
            DateTime.UtcNow);

        membership.InstitutionId.Should().Be(institutionId);
        membership.UserId.Should().Be(userId);
        membership.Role.Should().Be(role);
        membership.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_rejects_empty_ids_and_undefined_roles()
    {
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var emptyInstitution = () => SpeedReadingInstitutionMembership.Create(
            Guid.Empty,
            Guid.NewGuid(),
            SpeedReadingInstitutionMemberRole.Student,
            actorId,
            now);
        var emptyUser = () => SpeedReadingInstitutionMembership.Create(
            Guid.NewGuid(),
            Guid.Empty,
            SpeedReadingInstitutionMemberRole.Student,
            actorId,
            now);
        var undefinedRole = () => SpeedReadingInstitutionMembership.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (SpeedReadingInstitutionMemberRole)999,
            actorId,
            now);

        emptyInstitution.Should().Throw<ArgumentException>();
        emptyUser.Should().Throw<ArgumentException>();
        undefinedRole.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Update_changes_role_and_active_state_with_audit_actor()
    {
        var institutionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var membership = SpeedReadingInstitutionMembership.Create(
            institutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Student,
            userId,
            now);

        membership.ChangeRole(SpeedReadingInstitutionMemberRole.Teacher, actorId, now.AddMinutes(1));
        membership.SetActive(false, actorId, now.AddMinutes(2));

        membership.Role.Should().Be(SpeedReadingInstitutionMemberRole.Teacher);
        membership.IsActive.Should().BeFalse();
        membership.UpdatedBy.Should().Be(actorId.ToString());
    }

    [Fact]
    public void Ef_model_stores_speed_reading_memberships_without_cross_database_foreign_keys()
    {
        var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql("Host=localhost;Database=speed_reading_model_only;Username=unused;Password=unused")
            .Options;
        using var context = new OwnedSpeedReadingDbContext(options);
        var entity = context.Model.FindEntityType(typeof(SpeedReadingInstitutionMembership))!;

        entity.GetTableName().Should().Be("institution_memberships");
        entity.GetSchema().Should().Be("speed_reading");
        entity.GetForeignKeys().Should().BeEmpty();
        entity.GetIndexes().Should().Contain(index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name)
                .SequenceEqual(new[]
                {
                    nameof(SpeedReadingInstitutionMembership.InstitutionId),
                    nameof(SpeedReadingInstitutionMembership.UserId),
                    nameof(SpeedReadingInstitutionMembership.Role)
                }));
        context.Database.GetMigrations()
            .Should().Contain("20260925104304_AddSpeedReadingInstitutionMemberships")
            .And.Contain("20260925123518_SupportMultipleSpeedReadingInstitutionRoles");
    }

    [Fact]
    public async Task Membership_store_scopes_by_institution_and_role_and_updates_existing_memberships_idempotently()
    {
        await using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var store = new OwnedSpeedReadingInstitutionMemberships(context);
        var userId = Guid.NewGuid();
        var firstInstitutionId = Guid.NewGuid();
        var secondInstitutionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        (await store.SetMembershipAsync(
            firstInstitutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Student,
            isActive: false,
            actorId,
            DateTime.UtcNow,
            CancellationToken.None)).Should().BeFalse();
        var missingActor = () => store.SetMembershipAsync(
            firstInstitutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Student,
            isActive: true,
            Guid.Empty,
            DateTime.UtcNow,
            CancellationToken.None);
        await missingActor.Should().ThrowAsync<ArgumentException>();

        (await store.SetMembershipAsync(
            firstInstitutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Student,
            isActive: true,
            actorId,
            DateTime.UtcNow,
            CancellationToken.None)).Should().BeTrue();
        (await store.SetMembershipAsync(
            firstInstitutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Teacher,
            isActive: true,
            actorId,
            DateTime.UtcNow.AddMinutes(1),
            CancellationToken.None)).Should().BeTrue();
        (await store.SetMembershipAsync(
            secondInstitutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Student,
            isActive: true,
            actorId,
            DateTime.UtcNow.AddMinutes(2),
            CancellationToken.None)).Should().BeTrue();

        var firstMembers = await store.GetMembersAsync(firstInstitutionId, 1, 25, CancellationToken.None);
        var secondMembers = await store.GetMembersAsync(secondInstitutionId, 1, 25, CancellationToken.None);
        firstMembers.TotalCount.Should().Be(2);
        secondMembers.TotalCount.Should().Be(1);
        firstMembers.Items.Should().Contain(member =>
            member.UserId == userId && member.Role == SpeedReadingInstitutionMemberRole.Student);
        firstMembers.Items.Should().Contain(member =>
            member.UserId == userId && member.Role == SpeedReadingInstitutionMemberRole.Teacher);
        secondMembers.Items.Should().ContainSingle(member =>
            member.UserId == userId && member.Role == SpeedReadingInstitutionMemberRole.Student);
    }

    [Fact]
    public async Task Membership_store_keeps_multiple_roles_for_the_same_user_in_one_institution()
    {
        await using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var store = new OwnedSpeedReadingInstitutionMemberships(context);
        var institutionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        (await store.SetMembershipAsync(
            institutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Student,
            isActive: true,
            actorId,
            DateTime.UtcNow,
            CancellationToken.None)).Should().BeTrue();
        (await store.SetMembershipAsync(
            institutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Teacher,
            isActive: true,
            actorId,
            DateTime.UtcNow.AddMinutes(1),
            CancellationToken.None)).Should().BeTrue();

        var members = await store.GetMembersAsync(institutionId, 1, 25, CancellationToken.None);
        members.TotalCount.Should().Be(2);
        members.Items.Should().Contain(item => item.UserId == userId && item.Role == SpeedReadingInstitutionMemberRole.Student);
        members.Items.Should().Contain(item => item.UserId == userId && item.Role == SpeedReadingInstitutionMemberRole.Teacher);

        await store.SetMembershipAsync(
            institutionId,
            userId,
            SpeedReadingInstitutionMemberRole.Teacher,
            isActive: false,
            actorId,
            DateTime.UtcNow.AddMinutes(1),
            CancellationToken.None);
        members = await store.GetMembersAsync(institutionId, 1, 25, CancellationToken.None);
        members.Items.Should().ContainSingle(item =>
            item.UserId == userId && item.Role == SpeedReadingInstitutionMemberRole.Student && item.IsActive);
        members.Items.Should().ContainSingle(item =>
            item.UserId == userId && item.Role == SpeedReadingInstitutionMemberRole.Teacher && !item.IsActive);
    }

    [Fact]
    public async Task Membership_store_filters_roster_by_role_and_active_state_before_pagination()
    {
        await using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var store = new OwnedSpeedReadingInstitutionMemberships(context);
        var institutionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var activeStudentId = Guid.NewGuid();
        var inactiveStudentId = Guid.NewGuid();
        var activeTeacherId = Guid.NewGuid();

        await store.SetMembershipAsync(institutionId, activeStudentId, SpeedReadingInstitutionMemberRole.Student,
            true, actorId, DateTime.UtcNow, CancellationToken.None);
        await store.SetMembershipAsync(institutionId, inactiveStudentId, SpeedReadingInstitutionMemberRole.Student,
            true, actorId, DateTime.UtcNow, CancellationToken.None);
        await store.SetMembershipAsync(institutionId, activeTeacherId, SpeedReadingInstitutionMemberRole.Teacher,
            true, actorId, DateTime.UtcNow, CancellationToken.None);
        await store.SetMembershipAsync(institutionId, inactiveStudentId, SpeedReadingInstitutionMemberRole.Student,
            false, actorId, DateTime.UtcNow.AddMinutes(1), CancellationToken.None);

        var students = await store.GetMembersAsync(
            institutionId,
            1,
            1,
            CancellationToken.None,
            SpeedReadingInstitutionMemberRole.Student,
            isActive: true);

        students.TotalCount.Should().Be(1);
        students.Items.Should().ContainSingle(item => item.UserId == activeStudentId);
    }

    [Fact]
    public async Task Membership_store_filters_school_grade_without_conflating_adaptive_reading_level()
    {
        await using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var institutionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var gradeEightStudentId = Guid.NewGuid();
        var gradeNineStudentId = Guid.NewGuid();
        await context.InstitutionMemberships.AddRangeAsync(
            SpeedReadingInstitutionMembership.Create(institutionId, gradeEightStudentId, SpeedReadingInstitutionMemberRole.Student, actorId, DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(institutionId, gradeNineStudentId, SpeedReadingInstitutionMemberRole.Student, actorId, DateTime.UtcNow));
        context.UserProfiles.AddRange(
            SpeedReading.Domain.Profiles.SpeedReadingUserProfile.Import(
                Guid.NewGuid(), gradeEightStudentId, currentLevel: 3, 180, 70, 30, null, institutionId,
                true, DateTime.UtcNow, actorId.ToString(), null, null, gradeLevel: 8),
            SpeedReading.Domain.Profiles.SpeedReadingUserProfile.Import(
                Guid.NewGuid(), gradeNineStudentId, currentLevel: 8, 200, 75, 30, null, institutionId,
                true, DateTime.UtcNow, actorId.ToString(), null, null, gradeLevel: 9));
        await context.SaveChangesAsync();

        var page = await new OwnedSpeedReadingInstitutionMemberships(context).GetMembersAsync(
            institutionId, 1, 25, gradeLevel: 8);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(item =>
            item.UserId == gradeEightStudentId && item.GradeLevel == 8 && item.CurrentLevel == 3);
    }

    [Fact]
    public async Task Membership_store_can_resolve_one_member_without_loading_the_full_institution_roster()
    {
        await using var context = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var institutionId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var requestedStudentId = Guid.NewGuid();
        await context.InstitutionMemberships.AddRangeAsync(
            SpeedReadingInstitutionMembership.Create(institutionId, requestedStudentId, SpeedReadingInstitutionMemberRole.Student, actorId, DateTime.UtcNow),
            SpeedReadingInstitutionMembership.Create(institutionId, Guid.NewGuid(), SpeedReadingInstitutionMemberRole.Student, actorId, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var page = await new OwnedSpeedReadingInstitutionMemberships(context).GetMembersAsync(
            institutionId,
            1,
            1,
            role: SpeedReadingInstitutionMemberRole.Student,
            memberUserId: requestedStudentId);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle(item => item.UserId == requestedStudentId);
    }
}
