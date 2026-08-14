using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using Xunit;
using DomainUserRole = Identity.Domain.Entities.UserRole;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Exercises the EF-backed coaching read authorization matrix against real PostgreSQL.
/// These tests intentionally cover active-state and cross-tenant predicates that are
/// difficult to validate with policy/unit stubs alone.
/// </summary>
[Collection("Database")]
public sealed class CoachingStudentReadRepositoryTests : IAsyncLifetime
{
    private readonly PostgresFixture _postgresFixture;
    private IdentityDbContext? _dbContext;

    public CoachingStudentReadRepositoryTests(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_postgresFixture.ConnectionString)
            .Options;

        _dbContext = new IdentityDbContext(options);
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.Database.EnsureCreatedAsync();

        _dbContext.Roles.AddRange(
            Role.Create("Parent", "Integration test Parent", isSystemRole: true),
            Role.Create("Student", "Integration test Student", isSystemRole: true),
            Role.Create("InstitutionAdmin", "Integration test InstitutionAdmin", isSystemRole: true),
            Role.Create("InstitutionOwner", "Integration test InstitutionOwner", isSystemRole: true),
            Role.Create("SystemAdmin", "Integration test SystemAdmin", isSystemRole: true),
            Role.Create("Teacher", "Integration test Teacher", isSystemRole: true));
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_dbContext is null)
        {
            return;
        }

        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Parent_ShouldOnlyReadActiveChildren()
    {
        var institution = Institution.Create("Parent Scope School", InstitutionType.School);
        var parent = User.Create(Guid.NewGuid(), "parent-scope@example.test");
        var child = User.Create(Guid.NewGuid(), "child-scope@example.test");
        var unrelated = User.Create(Guid.NewGuid(), "unrelated-scope@example.test");
        var inactiveChild = User.Create(Guid.NewGuid(), "inactive-child@example.test");
        inactiveChild.Deactivate();

        AddRole(parent, "Parent");
        AddRole(child, "Student");
        AddRole(unrelated, "Student");
        AddRole(inactiveChild, "Student");

        var inactiveChildProfile = StudentProfile.Create(
            inactiveChild.Id,
            "Inactive",
            "Child",
            institution.Id,
            parent.Id);
        inactiveChildProfile.Deactivate();

        _dbContext!.Institutions.Add(institution);
        _dbContext.Users.AddRange(parent, child, unrelated, inactiveChild);
        _dbContext.ParentProfiles.Add(ParentProfile.Create(parent.Id, "Parent", "User"));
        _dbContext.StudentProfiles.AddRange(
            StudentProfile.Create(child.Id, "Child", "One", institution.Id, parent.Id),
            StudentProfile.Create(unrelated.Id, "Other", "Student", institution.Id),
            inactiveChildProfile);
        await _dbContext.SaveChangesAsync();

        var result = await Repository().AuthorizeCoachingStudentReadAsync(
            parent.Id,
            new[] { child.Id, unrelated.Id, inactiveChild.Id },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.AllowedStudentUserIds.Should().Equal(child.Id);
    }

    [Fact]
    public async Task InstitutionAdministrator_ShouldOnlyReadActiveStudentsInOwnedInstitution()
    {
        var institutionA = Institution.Create("Institution A", InstitutionType.School);
        var institutionB = Institution.Create("Institution B", InstitutionType.School);
        var administrator = User.Create(Guid.NewGuid(), "admin-scope@example.test");
        var studentA = User.Create(Guid.NewGuid(), "student-a@example.test");
        var studentB = User.Create(Guid.NewGuid(), "student-b@example.test");
        var inactiveStudentA = User.Create(Guid.NewGuid(), "inactive-a@example.test");
        inactiveStudentA.Deactivate();

        AddRole(administrator, "InstitutionAdmin");
        AddRole(administrator, "InstitutionOwner");
        AddRole(studentA, "Student");
        AddRole(studentB, "Student");
        AddRole(inactiveStudentA, "Student");

        _dbContext!.Institutions.AddRange(institutionA, institutionB);
        _dbContext.Users.AddRange(administrator, studentA, studentB, inactiveStudentA);
        _dbContext.InstitutionAdmins.Add(InstitutionAdmin.Create(
            administrator.Id,
            institutionA.Id,
            InstitutionAdminRole.Admin));
        _dbContext.StudentProfiles.AddRange(
            StudentProfile.Create(studentA.Id, "Student", "A", institutionA.Id),
            StudentProfile.Create(studentB.Id, "Student", "B", institutionB.Id),
            StudentProfile.Create(inactiveStudentA.Id, "Inactive", "A", institutionA.Id));
        await _dbContext.SaveChangesAsync();

        var result = await Repository().AuthorizeCoachingStudentReadAsync(
            administrator.Id,
            new[] { studentA.Id, studentB.Id, inactiveStudentA.Id },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.AllowedStudentUserIds.Should().Equal(studentA.Id);
    }

    [Fact]
    public async Task Teacher_ShouldReadAssignedStudentsOnlyWithinOwnInstitution()
    {
        var institutionA = Institution.Create("Teacher Institution A", InstitutionType.School);
        var institutionB = Institution.Create("Teacher Institution B", InstitutionType.School);
        var teacher = User.Create(Guid.NewGuid(), "teacher-scope@example.test");
        var assignedStudent = User.Create(Guid.NewGuid(), "assigned@example.test");
        var unassignedStudent = User.Create(Guid.NewGuid(), "unassigned@example.test");
        var otherInstitutionStudent = User.Create(Guid.NewGuid(), "other-institution@example.test");

        AddRole(teacher, "Teacher");
        AddRole(assignedStudent, "Student");
        AddRole(unassignedStudent, "Student");
        AddRole(otherInstitutionStudent, "Student");

        var teacherProfile = TeacherProfile.Create(teacher.Id, "Teacher", "One", institutionA.Id);
        var assignedProfile = StudentProfile.Create(assignedStudent.Id, "Assigned", "Student", institutionA.Id);
        var unassignedProfile = StudentProfile.Create(unassignedStudent.Id, "Unassigned", "Student", institutionA.Id);
        var otherInstitutionProfile = StudentProfile.Create(otherInstitutionStudent.Id, "Other", "Institution", institutionB.Id);

        _dbContext!.Institutions.AddRange(institutionA, institutionB);
        _dbContext.Users.AddRange(teacher, assignedStudent, unassignedStudent, otherInstitutionStudent);
        _dbContext.TeacherProfiles.Add(teacherProfile);
        _dbContext.StudentProfiles.AddRange(assignedProfile, unassignedProfile, otherInstitutionProfile);
        _dbContext.TeacherStudentAssignments.Add(TeacherStudentAssignment.Create(
            teacherProfile.Id,
            assignedProfile.Id,
            institutionA.Id));
        await _dbContext.SaveChangesAsync();

        var result = await Repository().AuthorizeCoachingStudentReadAsync(
            teacher.Id,
            new[] { assignedStudent.Id, unassignedStudent.Id, otherInstitutionStudent.Id },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.AllowedStudentUserIds.Should().Equal(assignedStudent.Id);

        teacherProfile.SetViewAllStudentsPermission(true);
        await _dbContext.SaveChangesAsync();

        var readAllResult = await Repository().AuthorizeCoachingStudentReadAsync(
            teacher.Id,
            new[] { assignedStudent.Id, unassignedStudent.Id, otherInstitutionStudent.Id },
            CancellationToken.None);

        readAllResult.Should().NotBeNull();
        readAllResult!.AllowedStudentUserIds.Should().BeEquivalentTo(
            new[] { assignedStudent.Id, unassignedStudent.Id });
    }

    [Fact]
    public async Task SystemAdministrator_ShouldReadActiveStudentsAcrossInstitutions()
    {
        var institutionA = Institution.Create("System Institution A", InstitutionType.School);
        var institutionB = Institution.Create("System Institution B", InstitutionType.School);
        var administrator = User.Create(Guid.NewGuid(), "system-scope@example.test");
        var studentA = User.Create(Guid.NewGuid(), "system-student-a@example.test");
        var studentB = User.Create(Guid.NewGuid(), "system-student-b@example.test");

        AddRole(administrator, "SystemAdmin");
        AddRole(studentA, "Student");
        AddRole(studentB, "Student");

        _dbContext!.Institutions.AddRange(institutionA, institutionB);
        _dbContext.Users.AddRange(administrator, studentA, studentB);
        _dbContext.StudentProfiles.AddRange(
            StudentProfile.Create(studentA.Id, "System", "A", institutionA.Id),
            StudentProfile.Create(studentB.Id, "System", "B", institutionB.Id));
        await _dbContext.SaveChangesAsync();

        var result = await Repository().AuthorizeCoachingStudentReadAsync(
            administrator.Id,
            new[] { studentA.Id, studentB.Id },
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.AllowedStudentUserIds.Should().BeEquivalentTo(new[] { studentA.Id, studentB.Id });
    }

    [Fact]
    public async Task DeactivatedViewer_ShouldBeDeniedEvenWithAnActiveProfile()
    {
        var institution = Institution.Create("Inactive Viewer School", InstitutionType.School);
        var teacher = User.Create(Guid.NewGuid(), "inactive-teacher@example.test");
        var student = User.Create(Guid.NewGuid(), "active-student@example.test");
        teacher.Deactivate();

        AddRole(teacher, "Teacher");
        AddRole(student, "Student");

        var teacherProfile = TeacherProfile.Create(teacher.Id, "Inactive", "Teacher", institution.Id);
        var studentProfile = StudentProfile.Create(student.Id, "Active", "Student", institution.Id);

        _dbContext!.Institutions.Add(institution);
        _dbContext.Users.AddRange(teacher, student);
        _dbContext.TeacherProfiles.Add(teacherProfile);
        _dbContext.StudentProfiles.Add(studentProfile);
        _dbContext.TeacherStudentAssignments.Add(TeacherStudentAssignment.Create(
            teacherProfile.Id,
            studentProfile.Id,
            institution.Id));
        await _dbContext.SaveChangesAsync();

        var result = await Repository().AuthorizeCoachingStudentReadAsync(
            teacher.Id,
            new[] { student.Id },
            CancellationToken.None);

        result.Should().BeNull();
    }

    private InstitutionRepository Repository() => new(_dbContext!);

    private void AddRole(User user, string roleName)
    {
        var role = _dbContext!.Roles.Local.Single(existing => existing.Name == roleName);

        _dbContext.UserRoles.Add(new DomainUserRole(user.Id, role.Id));
    }
}
