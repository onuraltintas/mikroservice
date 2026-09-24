using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.AcceptInvitation;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class InvitationInstitutionScopeTests
{
    [Fact]
    public async Task Teacher_with_role_but_no_profile_can_accept_institution_invitation_without_losing_student_profile()
    {
        var institution = Institution.Create("New", InstitutionType.School);
        var user = User.Create(Guid.NewGuid(), "teacher@example.com", "Teacher", "One");
        var student = StudentProfile.Create(user.Id, "Teacher", "One");
        var teacherRole = Role.Create("Teacher", "Teacher");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, teacherRole.Id));
        var invitation = Invitation.Create(Guid.NewGuid(), user.Email,
            InvitationType.TeacherToInstitution, institution.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(institution, user, student, teacherRole, invitation);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, user).Handle(
            new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.TeacherProfiles.Should().ContainSingle(profile =>
            profile.UserId == user.Id && profile.InstitutionId == institution.Id);
        context.StudentProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
        invitation.Status.Should().Be(InvitationStatus.Accepted);
    }

    [Fact]
    public async Task Independent_student_accepts_institution_invitation_with_teacher_assignment()
    {
        var institution = Institution.Create("New", InstitutionType.School);
        var teacherUser = User.Create(Guid.NewGuid(), "teacher@example.com", "Teacher", "One");
        var studentUser = User.Create(Guid.NewGuid(), "student@example.com", "Student", "One");
        var teacher = TeacherProfile.Create(teacherUser.Id, "Teacher", "One", institution.Id);
        var student = StudentProfile.Create(studentUser.Id, "Student", "One");
        var invitation = Invitation.Create(Guid.NewGuid(), studentUser.Email,
            InvitationType.StudentToInstitution, institution.Id, teacher.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(institution, teacherUser, studentUser, teacher, student, invitation);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, studentUser).Handle(
            new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.StudentProfiles.Single().InstitutionId.Should().Be(institution.Id);
        context.TeacherStudentAssignments.Should().ContainSingle(assignment =>
            assignment.TeacherId == teacher.Id && assignment.StudentId == student.Id &&
            assignment.InstitutionId == institution.Id && assignment.IsActive);
        invitation.Status.Should().Be(InvitationStatus.Accepted);
    }

    [Fact]
    public async Task Invitation_cannot_be_accepted_by_a_different_email()
    {
        var institution = Institution.Create("New", InstitutionType.School);
        var user = User.Create(Guid.NewGuid(), "other@example.com", "Other", "One");
        var invitation = Invitation.Create(Guid.NewGuid(), "invited@example.com",
            InvitationType.StudentToInstitution, institution.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(institution, user, StudentProfile.Create(user.Id, "Other", "One"), invitation);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, user).Handle(
            new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        context.StudentProfiles.Single().InstitutionId.Should().BeNull();
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task Independent_teacher_invitation_cannot_attach_a_student_from_an_institution()
    {
        var institution = Institution.Create("Current", InstitutionType.School);
        var teacherUser = User.Create(Guid.NewGuid(), "teacher@example.com", "Teacher", "One");
        var studentUser = User.Create(Guid.NewGuid(), "student@example.com", "Student", "One");
        var teacher = TeacherProfile.Create(teacherUser.Id, "Teacher", "One", isIndependent: true);
        var student = StudentProfile.Create(studentUser.Id, "Student", "One", institution.Id);
        var invitation = Invitation.Create(teacherUser.Id, studentUser.Email,
            InvitationType.StudentToTeacher, teacherId: teacher.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(institution, teacherUser, studentUser, teacher, student, invitation);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, studentUser).Handle(
            new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        context.TeacherStudentAssignments.Should().BeEmpty();
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    [Fact]
    public async Task Accepting_another_teacher_invitation_does_not_duplicate_an_active_assignment()
    {
        var teacherUser = User.Create(Guid.NewGuid(), "teacher@example.com", "Teacher", "One");
        var studentUser = User.Create(Guid.NewGuid(), "student@example.com", "Student", "One");
        var teacher = TeacherProfile.Create(teacherUser.Id, "Teacher", "One", isIndependent: true);
        var student = StudentProfile.Create(studentUser.Id, "Student", "One");
        var existing = TeacherStudentAssignment.Create(teacher.Id, student.Id);
        var invitation = Invitation.Create(teacherUser.Id, studentUser.Email,
            InvitationType.StudentToTeacher, teacherId: teacher.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(teacherUser, studentUser, teacher, student, existing, invitation);
        await context.SaveChangesAsync();

        var result = await CreateHandler(context, studentUser).Handle(
            new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        context.TeacherStudentAssignments.Should().ContainSingle();
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepting_another_institutions_invitation_does_not_silently_transfer_member(bool teacher)
    {
        var oldInstitution = Institution.Create("Current", InstitutionType.School);
        var newInstitution = Institution.Create("New", InstitutionType.School);
        var user = User.Create(Guid.NewGuid(), "member@example.com", "Member", "One");
        var invitation = Invitation.Create(Guid.NewGuid(), user.Email,
            teacher ? InvitationType.TeacherToInstitution : InvitationType.StudentToInstitution,
            newInstitution.Id);
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.AddRange(oldInstitution, newInstitution, user, invitation);
        if (teacher) context.TeacherProfiles.Add(TeacherProfile.Create(user.Id, "Member", "One", oldInstitution.Id));
        else context.StudentProfiles.Add(StudentProfile.Create(user.Id, "Member", "One", oldInstitution.Id));
        await context.SaveChangesAsync();

        var handler = CreateHandler(context, user);
        var result = await handler.Handle(new AcceptInvitationCommand(invitation.Id), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        (teacher ? context.TeacherProfiles.Single().InstitutionId : context.StudentProfiles.Single().InstitutionId)
            .Should().Be(oldInstitution.Id);
        invitation.Status.Should().Be(InvitationStatus.Pending);
    }

    private static AcceptInvitationCommandHandler CreateHandler(IdentityDbContext context, User user) => new(
        new InvitationRepository(context), new UserRepository(context),
        new TeacherRepository(context), new StudentRepository(context),
        new UnitOfWork(context), new CurrentUser(user.Id, user.Email),
        NullLogger<AcceptInvitationCommandHandler>.Instance);

    private sealed class CurrentUser(Guid id, string email) : ICurrentUserService
    {
        public Guid? UserId => id;
        public string? Email => email;
        public string? FullName => null;
        public IEnumerable<string> Roles => [];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
