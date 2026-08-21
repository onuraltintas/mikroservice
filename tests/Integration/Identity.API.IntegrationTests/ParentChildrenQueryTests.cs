using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Queries.GetMyChildren;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class ParentChildrenQueryTests
{
    [Fact]
    public async Task Returns_only_active_children_of_the_current_parent()
    {
        await using var context = CreateContext();
        var parentUser = User.Create(Guid.NewGuid(), "parent@test.local", "Parent", "User");
        var otherParent = User.Create(Guid.NewGuid(), "other-parent@test.local", "Other", "Parent");
        var activeChild = User.Create(Guid.NewGuid(), "child@test.local", "Active", "Child");
        var inactiveChild = User.Create(Guid.NewGuid(), "inactive-child@test.local", "Inactive", "Child");
        var otherChild = User.Create(Guid.NewGuid(), "other-child@test.local", "Other", "Child");

        context.Users.AddRange(parentUser, otherParent, activeChild, inactiveChild, otherChild);
        context.ParentProfiles.Add(ParentProfile.Create(parentUser.Id, "Parent", "User"));
        context.ParentProfiles.Add(ParentProfile.Create(otherParent.Id, "Other", "Parent"));
        var activeProfile = StudentProfile.Create(activeChild.Id, "Active", "Child", parentId: parentUser.Id);
        var inactiveProfile = StudentProfile.Create(inactiveChild.Id, "Inactive", "Child", parentId: parentUser.Id);
        inactiveProfile.Deactivate();
        var otherProfile = StudentProfile.Create(otherChild.Id, "Other", "Child", parentId: otherParent.Id);
        context.StudentProfiles.AddRange(activeProfile, inactiveProfile, otherProfile);
        await context.SaveChangesAsync();

        var handler = new GetMyChildrenQueryHandler(
            new ParentRepository(context),
            new StubCurrentUserService(parentUser.Id, "Parent"));

        var result = await handler.Handle(new GetMyChildrenQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(child => child.UserId == activeChild.Id);
        result.Value.Should().NotContain(child => child.UserId == inactiveChild.Id);
        result.Value.Should().NotContain(child => child.UserId == otherChild.Id);
    }

    [Fact]
    public async Task Rejects_non_parent_roles_without_querying_children()
    {
        await using var context = CreateContext();
        var student = User.Create(Guid.NewGuid(), "student@test.local", "Student", "User");
        context.Users.Add(student);
        await context.SaveChangesAsync();

        var handler = new GetMyChildrenQueryHandler(
            new ParentRepository(context),
            new StubCurrentUserService(student.Id, "Student"));

        var result = await handler.Handle(new GetMyChildrenQuery(), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Error.Forbidden");
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class StubCurrentUserService : ICurrentUserService
    {
        private readonly Guid _userId;
        private readonly string[] _roles;

        public StubCurrentUserService(Guid userId, params string[] roles)
        {
            _userId = userId;
            _roles = roles;
        }

        public Guid? UserId => _userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => _roles;
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
