using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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
    private NpgsqlDataSource? _dataSource;

    public CoachingStudentReadRepositoryTests(PostgresFixture postgresFixture)
    {
        _postgresFixture = postgresFixture;
    }

    public async Task InitializeAsync()
    {
        _dataSource = new NpgsqlDataSourceBuilder(_postgresFixture.ConnectionString)
            .EnableDynamicJson()
            .Build();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseNpgsql(_dataSource)
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
        try
        {
            if (_dbContext != null)
            {
                await _dbContext.Database.EnsureDeletedAsync();
            }
        }
        finally
        {
            try
            {
                if (_dbContext != null)
                {
                    await _dbContext.DisposeAsync();
                }
            }
            finally
            {
                if (_dataSource != null)
                {
                    await _dataSource.DisposeAsync();
                }
            }
        }
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
    public async Task TeacherStudentRoster_ShouldReturnOnlyActiveAssignedStudents()
    {
        var institution = Institution.Create("Teacher Roster School", InstitutionType.School);
        var teacher = User.Create(Guid.NewGuid(), "roster-teacher@example.test");
        var assignedStudent = User.Create(Guid.NewGuid(), "roster-assigned@example.test");
        var unassignedStudent = User.Create(Guid.NewGuid(), "roster-unassigned@example.test");
        var inactiveStudent = User.Create(Guid.NewGuid(), "roster-inactive@example.test");

        AddRole(teacher, "Teacher");
        AddRole(assignedStudent, "Student");
        AddRole(unassignedStudent, "Student");
        AddRole(inactiveStudent, "Student");

        var teacherProfile = TeacherProfile.Create(teacher.Id, "Roster", "Teacher", institution.Id);
        var assignedProfile = StudentProfile.Create(assignedStudent.Id, "Ada", "Assigned", institution.Id);
        assignedProfile.UpdateEducationInfo(gradeLevel: 8);
        var unassignedProfile = StudentProfile.Create(unassignedStudent.Id, "Una", "Assigned", institution.Id);
        var inactiveProfile = StudentProfile.Create(inactiveStudent.Id, "Ina", "Inactive", institution.Id);
        inactiveProfile.Deactivate();

        _dbContext!.Institutions.Add(institution);
        _dbContext.Users.AddRange(teacher, assignedStudent, unassignedStudent, inactiveStudent);
        _dbContext.TeacherProfiles.Add(teacherProfile);
        _dbContext.StudentProfiles.AddRange(assignedProfile, unassignedProfile, inactiveProfile);
        _dbContext.TeacherStudentAssignments.AddRange(
            TeacherStudentAssignment.Create(teacherProfile.Id, assignedProfile.Id, institution.Id, "Matematik"),
            TeacherStudentAssignment.Create(teacherProfile.Id, inactiveProfile.Id, institution.Id, "Fen"));
        await _dbContext.SaveChangesAsync();

        var result = await TeacherRepository().GetStudentsByTeacherUserIdAsync(
            teacher.Id,
            pageNumber: 1,
            pageSize: 25,
            searchTerm: null,
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(student =>
            student.UserId == assignedStudent.Id
            && student.FullName == "Ada Assigned"
            && student.GradeLevel == 8
            && student.Subject == "Matematik");
    }

    [Fact]
    public async Task TeacherStudentRoster_ShouldEnforceTenantAndActiveInstitution()
    {
        var institutionA = Institution.Create("Active Teacher School", InstitutionType.School);
        var institutionB = Institution.Create("Other Student School", InstitutionType.School);
        var teacher = User.Create(Guid.NewGuid(), "tenant-teacher@example.test");
        var sameInstitutionStudent = User.Create(Guid.NewGuid(), "same-institution@example.test");
        var crossInstitutionStudent = User.Create(Guid.NewGuid(), "cross-institution@example.test");

        AddRole(teacher, "Teacher");
        AddRole(sameInstitutionStudent, "Student");
        AddRole(crossInstitutionStudent, "Student");

        var teacherProfile = TeacherProfile.Create(teacher.Id, "Tenant", "Teacher", institutionA.Id);
        var sameInstitutionProfile = StudentProfile.Create(
            sameInstitutionStudent.Id,
            "Same",
            "Institution",
            institutionA.Id);
        var crossInstitutionProfile = StudentProfile.Create(
            crossInstitutionStudent.Id,
            "Cross",
            "Institution",
            institutionB.Id);

        _dbContext!.Institutions.AddRange(institutionA, institutionB);
        _dbContext.Users.AddRange(teacher, sameInstitutionStudent, crossInstitutionStudent);
        _dbContext.TeacherProfiles.Add(teacherProfile);
        _dbContext.StudentProfiles.AddRange(sameInstitutionProfile, crossInstitutionProfile);
        _dbContext.TeacherStudentAssignments.AddRange(
            TeacherStudentAssignment.Create(teacherProfile.Id, sameInstitutionProfile.Id, institutionA.Id),
            // Deliberately inconsistent data: the assignment claims institution A while the student belongs to B.
            TeacherStudentAssignment.Create(teacherProfile.Id, crossInstitutionProfile.Id, institutionA.Id));
        await _dbContext.SaveChangesAsync();

        var activeResult = await TeacherRepository().GetStudentsByTeacherUserIdAsync(
            teacher.Id, 1, 25, null, CancellationToken.None);

        activeResult.Items.Should().ContainSingle(student => student.UserId == sameInstitutionStudent.Id);

        institutionA.Deactivate();
        await _dbContext.SaveChangesAsync();

        var inactiveInstitutionResult = await TeacherRepository().GetStudentsByTeacherUserIdAsync(
            teacher.Id, 1, 25, null, CancellationToken.None);

        inactiveInstitutionResult.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task TeacherStudentRoster_ShouldUseStableOrderingAndSubjectSelection()
    {
        var institution = Institution.Create("Deterministic Roster School", InstitutionType.School);
        var teacher = User.Create(Guid.NewGuid(), "deterministic-teacher@example.test");
        var firstStudent = User.Create(Guid.NewGuid(), "deterministic-first@example.test");
        var secondStudent = User.Create(Guid.NewGuid(), "deterministic-second@example.test");

        AddRole(teacher, "Teacher");
        AddRole(firstStudent, "Student");
        AddRole(secondStudent, "Student");

        var teacherProfile = TeacherProfile.Create(teacher.Id, "Deterministic", "Teacher", institution.Id);
        var firstProfile = StudentProfile.Create(firstStudent.Id, "Same", "Name", institution.Id);
        var secondProfile = StudentProfile.Create(secondStudent.Id, "Same", "Name", institution.Id);

        _dbContext!.Institutions.Add(institution);
        _dbContext.Users.AddRange(teacher, firstStudent, secondStudent);
        _dbContext.TeacherProfiles.Add(teacherProfile);
        _dbContext.StudentProfiles.AddRange(firstProfile, secondProfile);
        _dbContext.TeacherStudentAssignments.AddRange(
            TeacherStudentAssignment.Create(teacherProfile.Id, firstProfile.Id, institution.Id, "Türkçe"),
            TeacherStudentAssignment.Create(teacherProfile.Id, firstProfile.Id, institution.Id, "Matematik"),
            TeacherStudentAssignment.Create(teacherProfile.Id, secondProfile.Id, institution.Id, "Fen"));
        await _dbContext.SaveChangesAsync();

        var result = await TeacherRepository().GetStudentsByTeacherUserIdAsync(
            teacher.Id, 1, 25, null, CancellationToken.None);

        result.Items.Select(student => student.UserId)
            .Should().Equal(new[] { firstStudent.Id, secondStudent.Id }.OrderBy(id => id));
        result.Items.Single(student => student.UserId == firstStudent.Id).Subject.Should().Be("Matematik");
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

    [Fact]
    public async Task CoachingReportRoster_ShouldRespectInstitutionGradeAndActiveState()
    {
        var institutionA = Institution.Create("Report Institution A", InstitutionType.School);
        var institutionB = Institution.Create("Report Institution B", InstitutionType.School);
        var administrator = User.Create(Guid.NewGuid(), "report-admin@example.test");
        var gradeEight = User.Create(Guid.NewGuid(), "grade-eight@example.test");
        var gradeNine = User.Create(Guid.NewGuid(), "grade-nine@example.test");
        var inactive = User.Create(Guid.NewGuid(), "inactive-report@example.test");
        inactive.Deactivate();
        var otherInstitution = User.Create(Guid.NewGuid(), "other-report@example.test");

        AddRole(administrator, "InstitutionAdmin");
        AddRole(gradeEight, "Student");
        AddRole(gradeNine, "Student");
        AddRole(inactive, "Student");
        AddRole(otherInstitution, "Student");

        var inactiveProfile = StudentProfile.Create(inactive.Id, "Inactive", "Report", institutionA.Id);
        inactiveProfile.UpdateEducationInfo(gradeLevel: 8);

        var gradeEightProfile = StudentProfile.Create(gradeEight.Id, "Grade", "Eight", institutionA.Id);
        gradeEightProfile.UpdateEducationInfo(gradeLevel: 8);
        var gradeNineProfile = StudentProfile.Create(gradeNine.Id, "Grade", "Nine", institutionA.Id);
        gradeNineProfile.UpdateEducationInfo(gradeLevel: 9);

        _dbContext!.Institutions.AddRange(institutionA, institutionB);
        _dbContext.Users.AddRange(administrator, gradeEight, gradeNine, inactive, otherInstitution);
        _dbContext.InstitutionAdmins.Add(InstitutionAdmin.Create(
            administrator.Id,
            institutionA.Id,
            InstitutionAdminRole.Admin));
        _dbContext.StudentProfiles.AddRange(
            gradeEightProfile,
            gradeNineProfile,
            inactiveProfile,
            StudentProfile.Create(otherInstitution.Id, "Other", "Institution", institutionB.Id));
        await _dbContext.SaveChangesAsync();

        var result = await Repository().GetCoachingReportStudentUserIdsAsync(
            administrator.Id,
            institutionA.Id,
            8,
            CancellationToken.None);

        result.Should().Equal(gradeEight.Id);
    }

    [Fact]
    public async Task CoachingReportRosterPage_ShouldReturnBoundedOrderedPageAndTotalCount()
    {
        var institution = Institution.Create("Paged Report Institution", InstitutionType.School);
        var administrator = User.Create(Guid.NewGuid(), "paged-report-admin@example.test");
        AddRole(administrator, "InstitutionAdmin");

        var students = Enumerable.Range(0, 3)
            .Select(index => User.Create(Guid.NewGuid(), $"paged-student-{index}@example.test"))
            .OrderBy(user => user.Id)
            .ToArray();
        foreach (var student in students)
        {
            AddRole(student, "Student");
            var profile = StudentProfile.Create(student.Id, "Paged", "Student", institution.Id);
            profile.UpdateEducationInfo(gradeLevel: 8);
            _dbContext!.StudentProfiles.Add(profile);
        }

        _dbContext!.Institutions.Add(institution);
        _dbContext.Users.AddRange(administrator);
        _dbContext.Users.AddRange(students);
        _dbContext.InstitutionAdmins.Add(InstitutionAdmin.Create(
            administrator.Id,
            institution.Id,
            InstitutionAdminRole.Admin));
        await _dbContext.SaveChangesAsync();

        var result = await Repository().GetCoachingReportStudentPageAsync(
            administrator.Id,
            institution.Id,
            8,
            pageNumber: 2,
            pageSize: 2,
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(3);
        result.StudentUserIds.Should().Equal(students.Skip(2).Select(student => student.Id));
    }

    private InstitutionRepository Repository() => new(_dbContext!);

    private TeacherRepository TeacherRepository() => new(_dbContext!);

    private void AddRole(User user, string roleName)
    {
        var role = _dbContext!.Roles.Local.Single(existing => existing.Name == roleName);

        _dbContext.UserRoles.Add(new DomainUserRole(user.Id, role.Id));
    }
}
