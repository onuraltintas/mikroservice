using FluentAssertions;
using Identity.Application.Commands.UpdateUser;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class UpdateUserRoleProfileTests
{
    [Fact]
    public async Task Editing_dual_role_user_repairs_both_role_profiles()
    {
        await using var context = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = User.Create(Guid.NewGuid(), "member@example.test", "Member", "One");
        var teacherRole = Role.Create("Teacher", "Teacher");
        var studentRole = Role.Create("Student", "Student");
        user.AddRole(new UserRole(user.Id, teacherRole.Id));
        user.AddRole(new UserRole(user.Id, studentRole.Id));
        context.AddRange(user, teacherRole, studentRole);
        await context.SaveChangesAsync();
        var handler = new UpdateUserCommandHandler(
            new UserRepository(context), new UnitOfWork(context),
            new StudentRepository(context), new TeacherRepository(context),
            new InstitutionRepository(context));

        var result = await handler.Handle(new UpdateUserCommand(
            user.Id, "Member", "Updated", null, UpdateRoleProfile: true), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        context.TeacherProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
        context.StudentProfiles.Should().ContainSingle(profile => profile.UserId == user.Id);
    }
}
