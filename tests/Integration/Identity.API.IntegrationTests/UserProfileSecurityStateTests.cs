using System.Security.Claims;
using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class UserProfileSecurityStateTests
{
    [Fact]
    public async Task Profile_ShouldExposeActiveEmailAndMfaSecurityState()
    {
        await using var context = new IdentityDbContext(
            new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var user = User.Create(Guid.NewGuid(), "admin@example.test", "Admin", "User");
        user.ConfirmEmail();
        user.EnableMfa("protected", ["hash"], DateTimeOffset.UtcNow);
        user.Deactivate();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var result = await new GetUserProfileQueryHandler(
            new UserRepository(context),
            new TeacherRepository(context),
            new StudentRepository(context),
            new InstitutionRepository(context),
            new CurrentUser(user.Id))
            .Handle(new GetUserProfileQuery(user.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsActive.Should().BeFalse();
        result.Value.EmailConfirmed.Should().BeTrue();
        result.Value.MfaEnabled.Should().BeTrue();
    }

    private sealed class CurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "admin@example.test";
        public string? FullName => "Admin User";
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
