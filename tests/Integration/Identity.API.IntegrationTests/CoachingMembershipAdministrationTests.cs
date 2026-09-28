using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.UpdateInstitutionStudent;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingMembershipAdministrationTests
{
    [Fact]
    public void Administrative_membership_endpoint_requires_system_admin_and_manage_permission()
    {
        var controller = typeof(Identity.API.Controllers.CoachingInstitutionMembersController);
        controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Should().Contain(attribute => attribute.Roles == "SystemAdmin");
        controller.GetCustomAttributes(typeof(EduPlatform.Shared.Security.Authorization.HasPermissionAttribute), true)
            .Should().NotBeEmpty();
    }
    [Theory]
    [InlineData("SystemAdmin", true)]
    [InlineData("InstitutionAdmin", false)]
    [InlineData("Teacher", false)]
    public async Task Explicit_institution_requires_system_admin(string role, bool allowed)
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var institution = Institution.Create("School", InstitutionType.School);
        var user = User.Create(Guid.NewGuid(), "student@test.local", "Test", "Student");
        var student = StudentProfile.Create(user.Id, "Test", "Student", institution.Id);
        db.AddRange(institution, user, student);
        await db.SaveChangesAsync();
        var handler = new UpdateInstitutionStudentCommandHandler(new InstitutionRepository(db),
            new StudentRepository(db), new TeacherRepository(db), new UnitOfWork(db), new Actor(role));

        var result = await handler.Handle(new UpdateInstitutionStudentCommand(student.UserId, 8, null)
            { InstitutionId = institution.Id }, CancellationToken.None);

        result.IsSuccess.Should().Be(allowed);
        student.GradeLevel.Should().Be(allowed ? 8 : null);
    }

    [Fact]
    public async Task System_admin_cannot_modify_student_from_another_institution()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var institution = Institution.Create("School", InstitutionType.School);
        var otherInstitution = Institution.Create("Other", InstitutionType.School);
        var user = User.Create(Guid.NewGuid(), "student@test.local", "Test", "Student");
        var student = StudentProfile.Create(user.Id, "Test", "Student", otherInstitution.Id);
        db.AddRange(institution, otherInstitution, user, student);
        await db.SaveChangesAsync();
        var handler = new UpdateInstitutionStudentCommandHandler(new InstitutionRepository(db),
            new StudentRepository(db), new TeacherRepository(db), new UnitOfWork(db), new Actor("SystemAdmin"));
        var result = await handler.Handle(new UpdateInstitutionStudentCommand(student.UserId, 8, null)
            { InstitutionId = institution.Id }, CancellationToken.None);
        result.IsFailure.Should().BeTrue();
        student.GradeLevel.Should().BeNull();
    }

    private sealed class Actor(string role) : ICurrentUserService
    {
        public Guid? UserId { get; } = Guid.NewGuid();
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => [role];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
