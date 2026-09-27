using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.Persistence;
using EduPlatform.Shared.Contracts.Reporting;

namespace SpeedReading.Application.UnitTests;

public sealed class SpeedReadingTeacherAccessTests
{
    [Fact]
    public async Task Teacher_scope_contains_only_active_speed_reading_assignments()
    {
        await using var context = CreateContext();
        var institution = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var visibleStudent = Guid.NewGuid();
        var inactiveStudent = Guid.NewGuid();
        AddMembership(context, institution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institution, visibleStudent, SpeedReadingInstitutionMemberRole.Student);
        var inactive = SpeedReadingInstitutionMembership.Create(
            institution, inactiveStudent, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), DateTime.UtcNow);
        inactive.SetActive(false, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1));
        context.InstitutionMemberships.Add(inactive);
        AddAssignment(context, institution, teacher, visibleStudent);
        AddAssignment(context, institution, teacher, inactiveStudent);
        await context.SaveChangesAsync();

        var access = CreateAccess(context, activeInstitutions: [institution]);
        var scope = await access.GetStudentScopeAsync(teacher);

        scope.Should().NotBeNull();
        scope!.InstitutionIds.Should().BeEmpty();
        scope.StudentUserIds.Should().BeEquivalentTo([visibleStudent]);
        scope.TotalStudents.Should().Be(1);
    }

    [Fact]
    public async Task Institution_admin_scope_is_limited_to_managed_speed_reading_institutions()
    {
        await using var context = CreateContext();
        var firstInstitution = Guid.NewGuid();
        var secondInstitution = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var firstStudent = Guid.NewGuid();
        var secondStudent = Guid.NewGuid();
        AddMembership(context, firstInstitution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, secondInstitution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, firstInstitution, firstStudent, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, secondInstitution, secondStudent, SpeedReadingInstitutionMemberRole.Student);
        AddAssignment(context, firstInstitution, teacher, firstStudent);
        AddAssignment(context, secondInstitution, teacher, secondStudent);
        await context.SaveChangesAsync();
        var managerAuthorization = new StubInstitutionManagerAuthorization(firstInstitution);

        var scope = await CreateAccess(context, managerAuthorization, firstInstitution, secondInstitution).GetStudentScopeAsync(
            Guid.NewGuid(),
            teacher,
            isInstitutionAdmin: true);

        scope.Should().NotBeNull();
        scope!.StudentUserIds.Should().BeEquivalentTo([firstStudent]);
        managerAuthorization.CheckedInstitutions.Should().BeEquivalentTo([firstInstitution, secondInstitution]);
    }

    [Fact]
    public async Task Unauthorized_viewer_cannot_request_another_teachers_scope()
    {
        await using var context = CreateContext();
        var institution = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var student = Guid.NewGuid();
        AddMembership(context, institution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institution, student, SpeedReadingInstitutionMemberRole.Student);
        AddAssignment(context, institution, teacher, student);
        await context.SaveChangesAsync();

        var scope = await CreateAccess(context, activeInstitutions: [institution]).GetStudentScopeAsync(Guid.NewGuid(), teacher);

        scope.Should().BeNull();
    }

    [Fact]
    public async Task System_admin_can_view_only_the_target_teachers_local_assignments()
    {
        await using var context = CreateContext();
        var institution = Guid.NewGuid();
        var targetTeacher = Guid.NewGuid();
        var anotherTeacher = Guid.NewGuid();
        var targetStudent = Guid.NewGuid();
        var unrelatedStudent = Guid.NewGuid();
        AddMembership(context, institution, targetTeacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institution, anotherTeacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institution, targetStudent, SpeedReadingInstitutionMemberRole.Student);
        AddMembership(context, institution, unrelatedStudent, SpeedReadingInstitutionMemberRole.Student);
        AddAssignment(context, institution, targetTeacher, targetStudent);
        AddAssignment(context, institution, anotherTeacher, unrelatedStudent);
        await context.SaveChangesAsync();

        var scope = await CreateAccess(context, activeInstitutions: [institution]).GetStudentScopeAsync(
            Guid.NewGuid(), targetTeacher, isSystemAdmin: true);

        scope.Should().NotBeNull();
        scope!.StudentUserIds.Should().BeEquivalentTo([targetStudent]);
    }

    [Fact]
    public async Task Read_authorization_does_not_include_students_linked_only_in_another_service()
    {
        await using var context = CreateContext();
        var institution = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var locallyAssignedStudent = Guid.NewGuid();
        var coachingOnlyStudent = Guid.NewGuid();
        AddMembership(context, institution, teacher, SpeedReadingInstitutionMemberRole.Teacher);
        AddMembership(context, institution, locallyAssignedStudent, SpeedReadingInstitutionMemberRole.Student);
        AddAssignment(context, institution, teacher, locallyAssignedStudent);
        await context.SaveChangesAsync();

        var ids = await CreateAccess(context, activeInstitutions: [institution])
            .GetReadableStudentIdsAsync(teacher, [locallyAssignedStudent, coachingOnlyStudent]);

        ids.Should().BeEquivalentTo([locallyAssignedStudent]);
    }

    [Fact]
    public async Task Independent_teacher_can_read_speed_reading_students_without_institution_memberships()
    {
        await using var context = CreateContext();
        var teacher = Guid.NewGuid();
        var student = Guid.NewGuid();
        context.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
            null, teacher, student, teacher, DateTime.UtcNow));
        await context.SaveChangesAsync();

        var access = CreateAccess(context);

        (await access.GetReadableStudentIdsAsync(teacher, [student])).Should().BeEquivalentTo([student]);
        var scope = await access.GetStudentScopeAsync(teacher);
        scope.Should().NotBeNull();
        scope!.StudentUserIds.Should().BeEquivalentTo([student]);
    }

    [Fact]
    public async Task Institution_scope_includes_all_active_students_only_for_managed_institution()
    {
        await using var context = CreateContext();
        var institution = Guid.NewGuid();
        var inactiveInstitution = Guid.NewGuid();
        var activeStudent = Guid.NewGuid();
        var inactiveStudent = Guid.NewGuid();
        AddMembership(context, institution, activeStudent, SpeedReadingInstitutionMemberRole.Student);
        var inactiveMembership = SpeedReadingInstitutionMembership.Create(
            institution, inactiveStudent, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), DateTime.UtcNow);
        inactiveMembership.SetActive(false, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1));
        context.InstitutionMemberships.Add(inactiveMembership);
        await context.SaveChangesAsync();

        var viewer = Guid.NewGuid();
        var access = new OwnedSpeedReadingTeacherAccess(
            context,
            new StubInstitutionDirectory([institution], [inactiveInstitution]),
            new StubInstitutionManagerAuthorization(institution));

        var scope = await access.GetInstitutionStudentScopeAsync(viewer, institution);
        var unmanagedScope = await access.GetInstitutionStudentScopeAsync(viewer, inactiveInstitution);

        scope.Should().NotBeNull();
        scope!.InstitutionIds.Should().BeEquivalentTo([institution]);
        scope.StudentUserIds.Should().BeEmpty();
        scope.TotalStudents.Should().Be(1);
        unmanagedScope.Should().BeNull();
    }

    [Fact]
    public async Task Institution_student_access_requires_active_student_membership_in_managed_institution()
    {
        await using var context = CreateContext();
        var institution = Guid.NewGuid();
        var student = Guid.NewGuid();
        var inactiveStudent = Guid.NewGuid();
        AddMembership(context, institution, student, SpeedReadingInstitutionMemberRole.Student);
        var inactiveMembership = SpeedReadingInstitutionMembership.Create(
            institution, inactiveStudent, SpeedReadingInstitutionMemberRole.Student, Guid.NewGuid(), DateTime.UtcNow);
        inactiveMembership.SetActive(false, Guid.NewGuid(), DateTime.UtcNow.AddMinutes(1));
        context.InstitutionMemberships.Add(inactiveMembership);
        await context.SaveChangesAsync();

        var manager = Guid.NewGuid();
        var access = CreateAccess(context, new StubInstitutionManagerAuthorization(institution), institution);

        (await access.CanReadInstitutionStudentAsync(manager, institution, student)).Should().BeTrue();
        (await access.CanReadInstitutionStudentAsync(manager, institution, inactiveStudent)).Should().BeFalse();
        (await access.CanReadInstitutionStudentAsync(manager, institution, Guid.NewGuid())).Should().BeFalse();
        var unauthorizedAccess = CreateAccess(context, activeInstitutions: [institution]);
        (await unauthorizedAccess.CanReadInstitutionStudentAsync(Guid.NewGuid(), institution, student)).Should().BeFalse();
    }

    private static OwnedSpeedReadingTeacherAccess CreateAccess(
        OwnedSpeedReadingDbContext context,
        StubInstitutionManagerAuthorization? authorization = null,
        params Guid[] activeInstitutions) => new(
            context,
            new StubInstitutionDirectory(activeInstitutions),
            authorization ?? new StubInstitutionManagerAuthorization());

    private static OwnedSpeedReadingDbContext CreateContext() => new(
        new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddMembership(
        OwnedSpeedReadingDbContext context,
        Guid institutionId,
        Guid userId,
        SpeedReadingInstitutionMemberRole role) =>
        context.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(
            institutionId, userId, role, Guid.NewGuid(), DateTime.UtcNow));

    private static void AddAssignment(
        OwnedSpeedReadingDbContext context,
        Guid institutionId,
        Guid teacherId,
        Guid studentId) =>
        context.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
            institutionId, teacherId, studentId, Guid.NewGuid(), DateTime.UtcNow));

    private sealed class StubInstitutionDirectory(
        Guid[] activeInstitutions,
        Guid[]? inactiveInstitutions = null) : ISpeedReadingInstitutionDirectory
    {
        public Task<SpeedReadingInstitutionScopeResponse> GetInstitutionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new SpeedReadingInstitutionScopeResponse(activeInstitutions
                .Select(id => new SpeedReadingInstitutionScopeItem(id, "Test institution", true))
                .Concat((inactiveInstitutions ?? []).Select(id => new SpeedReadingInstitutionScopeItem(id, "Inactive institution", false)))
                .ToArray()));
    }

    private sealed class StubInstitutionManagerAuthorization(params Guid[] managedInstitutions)
        : ISpeedReadingInstitutionAdministrationAuthorization
    {
        public List<Guid> CheckedInstitutions { get; } = [];

        public Task<bool> CanManageAsync(
            Guid userId,
            Guid institutionId,
            CancellationToken cancellationToken = default)
        {
            CheckedInstitutions.Add(institutionId);
            return Task.FromResult(managedInstitutions.Contains(institutionId));
        }
    }
}
