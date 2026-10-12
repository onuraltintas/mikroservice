using System.Security.Claims;
using System.Reflection;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.JoinCourseManagement;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CourseManagementJoinTests
{
    [Fact]
    public async Task ExistingAccountJoinsOnlyCourseProductAndRetriesDoNotReassignRole()
    {
        await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = User.Create(Guid.NewGuid(), "existing@example.test");
        user.ConfirmEmail();
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        var role = Role.Create("Teacher", "Teacher");
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, PlatformProduct.Coaching));
        db.Users.Add(user);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var users = DispatchProxy.Create<IUserRepository, RecordingUserRepository>();
        var recorder = (RecordingUserRepository)(object)users;
        recorder.User = user;
        recorder.Context = db;
        var roles = DispatchProxy.Create<IRoleRepository, CourseRoleRepository>();
        ((CourseRoleRepository)(object)roles).Context = db;
        var current = new CurrentAccount(user.Id);
        var handler = new JoinCourseManagementCommandHandler(users, roles, new Identity.Infrastructure.Repositories.UnitOfWork(db), current);
        Assert.True((await handler.Handle(new("Teacher"), CancellationToken.None)).IsSuccess);
        Assert.True((await handler.Handle(new("Teacher"), CancellationToken.None)).IsSuccess);
        Assert.True(user.HasProductAccess(PlatformProduct.Coaching));
        Assert.True(user.HasProductAccess(PlatformProduct.CourseManagement));
        Assert.False(user.HasProductAccess(PlatformProduct.SpeedReading));
        Assert.Single(user.Roles.Where(item => item.Product == PlatformProduct.CourseManagement));
        Assert.Empty(recorder.RevokedProducts);
        var courseRole = Assert.Single(await db.Roles.Where(item => item.Name == "CourseTeacher").ToListAsync());
        Assert.Empty(courseRole.Permissions);
    }

    [Theory]
    [InlineData("SystemAdmin")]
    [InlineData("Editor")]
    [InlineData("InstitutionAdmin")]
    [InlineData("")]
    public async Task SelfJoinCannotGrantAdministrativeRoles(string role)
    {
        var handler = new JoinCourseManagementCommandHandler(null!, null!, null!, new CurrentAccount(Guid.NewGuid()));
        Assert.Equal("CourseJoin.InvalidRole", (await handler.Handle(new(role), CancellationToken.None)).Error.Code);
    }

    public class CourseRoleRepository : DispatchProxy
    {
        public IdentityDbContext Context { get; set; } = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IRoleRepository.GetByNameAsync) => Context.Roles.SingleOrDefaultAsync(item => item.Name == (string)args![0]!),
            nameof(IRoleRepository.AddAsync) => AddRole((Role)args![0]!),
            _ => throw new NotSupportedException(method?.Name)
        };
        private Task AddRole(Role role) { Context.Roles.Add(role); return Task.CompletedTask; }
    }

    public class RecordingUserRepository : ProductScopedRoleAssignmentTests.UserRepositoryProxy
    {
        public List<PlatformProduct> RevokedProducts { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IUserRepository.RevokeActiveRefreshTokensForProductAsync))
                RevokedProducts.Add((PlatformProduct)args![1]!);
            return base.Invoke(method, args);
        }
    }

    private sealed class CurrentAccount(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "existing@example.test";
        public string? FullName => "Existing Account";
        public IEnumerable<string> Roles => ["Teacher"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}
