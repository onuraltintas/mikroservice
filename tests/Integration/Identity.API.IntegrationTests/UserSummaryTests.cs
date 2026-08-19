using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Queries.GetAllUsers;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class UserSummaryTests
{
    [Fact]
    public async Task Summary_ReturnsAllUsersWithoutPaginationTruncation()
    {
        await using var context = CreateContext();
        context.Users.AddRange(
            User.Create(Guid.NewGuid(), "active@test.local", "Active", "User"),
            User.Create(Guid.NewGuid(), "inactive@test.local", "Inactive", "User"));
        var inactive = context.Users.Local.Single(user => user.Email == "inactive@test.local");
        inactive.Deactivate();
        await context.SaveChangesAsync();

        var handler = new GetUserSummaryQueryHandler(
            new UserRepository(context),
            new InstitutionRepository(context),
            new StubCurrentUserService(Guid.NewGuid(), "SystemAdmin"));

        var result = await handler.Handle(new GetUserSummaryQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalUsers.Should().Be(2);
        result.Value.ActiveUsers.Should().Be(1);
        result.Value.InactiveUsers.Should().Be(1);
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
