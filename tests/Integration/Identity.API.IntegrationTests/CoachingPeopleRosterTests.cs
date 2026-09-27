using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using DomainUserRole = Identity.Domain.Entities.UserRole;

namespace Identity.API.IntegrationTests;

public sealed class CoachingPeopleRosterTests
{
    [Fact]
    public async Task RosterSearchAndTeacherFilter_StayInsideAuthorizedInstitution()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var context = new IdentityDbContext(options);
        var institutionRole = Role.Create("InstitutionAdmin", "Administrator", true);
        var studentRole = Role.Create("Student", "Student", true);
        var teacherRole = Role.Create("Teacher", "Teacher", true);
        context.Roles.AddRange(institutionRole, studentRole, teacherRole);

        var own = Institution.Create("Own", InstitutionType.School);
        var outside = Institution.Create("Outside", InstitutionType.School);
        var admin = User.Create(Guid.NewGuid(), "admin@example.test");
        var teacher = User.Create(Guid.NewGuid(), "ayse.teacher@example.test");
        var student = User.Create(Guid.NewGuid(), "ali.student@example.test");
        var outsideStudent = User.Create(Guid.NewGuid(), "outside.student@example.test");
        context.Institutions.AddRange(own, outside);
        context.Users.AddRange(admin, teacher, student, outsideStudent);
        context.UserRoles.AddRange(
            new DomainUserRole(admin.Id, institutionRole.Id),
            new DomainUserRole(teacher.Id, teacherRole.Id),
            new DomainUserRole(student.Id, studentRole.Id),
            new DomainUserRole(outsideStudent.Id, studentRole.Id));
        context.InstitutionAdmins.Add(InstitutionAdmin.Create(admin.Id, own.Id, InstitutionAdminRole.Admin, PlatformProduct.Coaching));
        var teacherProfile = TeacherProfile.Create(teacher.Id, "Ayşe", "Öğretmen", own.Id);
        var studentProfile = StudentProfile.Create(student.Id, "Ali", "Öğrenci", own.Id);
        context.TeacherProfiles.Add(teacherProfile);
        context.StudentProfiles.AddRange(
            studentProfile,
            StudentProfile.Create(outsideStudent.Id, "Ali", "Dış", outside.Id));
        context.TeacherStudentAssignments.Add(TeacherStudentAssignment.Create(
            teacherProfile.Id, studentProfile.Id, own.Id));
        await context.SaveChangesAsync();

        var repository = new InstitutionRepository(context);
        var students = await repository.GetCoachingReportStudentPageAsync(
            admin.Id, own.Id, null, 1, 25, CancellationToken.None,
            search: "Ali", teacherUserId: teacher.Id);
        var teachers = await repository.GetCoachingReportTeacherPageAsync(
            admin.Id, own.Id, 1, 25, "Ayşe", CancellationToken.None);
        var foreign = await repository.GetCoachingReportTeacherPageAsync(
            admin.Id, outside.Id, 1, 25, null, CancellationToken.None);

        students!.StudentUserIds.Should().ContainSingle().Which.Should().Be(student.Id);
        teachers!.Teachers.Should().ContainSingle().Which.UserId.Should().Be(teacher.Id);
        foreign.Should().BeNull();
    }
}
