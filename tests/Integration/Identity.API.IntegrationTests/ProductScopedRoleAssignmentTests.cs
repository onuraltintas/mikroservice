using System.Security.Claims;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class ProductScopedRoleAssignmentTests
{
    [Fact]
    public async Task AssignRoleForProduct_ShouldGrantRoleAndOnlyTheSelectedProductAccess()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var userId = Guid.NewGuid();
        var role = Role.Create("Teacher", "Teacher");
        var user = User.Create(userId, "teacher@example.test");
        user.AddRole(new Identity.Domain.Entities.UserRole(userId, role.Id, PlatformProduct.Coaching));

        await using var context = new IdentityDbContext(options);
        context.Users.Add(user);
        context.Roles.Add(role);
        await context.SaveChangesAsync();

        var userRepository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
        var userRepositoryProxy = (UserRepositoryProxy)(object)userRepository;
        userRepositoryProxy.User = await context.Users.SingleAsync();
        userRepositoryProxy.Context = context;
        var roleRepository = DispatchProxy.Create<IRoleRepository, RoleRepositoryProxy>();
        ((RoleRepositoryProxy)(object)roleRepository).Role = role;
        var logger = new CapturingLogger();
        var service = new LocalIdentityService(
            userRepository,
            passwordHasher: null!,
            new UnitOfWork(context),
            logger,
            tokenService: null!,
            roleRepository,
            context,
            new SystemAdministratorCurrentUserService());

        var result = await service.AssignRoleForProductAsync(
            userId,
            "Teacher",
            PlatformProduct.SpeedReading,
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(logger.Exception?.ToString() ?? (result.IsFailure ? result.Error.Description : null));
        var savedUser = await context.Users.Include(candidate => candidate.ProductAccesses)
            .Include(candidate => candidate.Roles).ThenInclude(userRole => userRole.Role)
            .SingleAsync(candidate => candidate.Id == userId);
        savedUser.Should().NotBeNull();
        savedUser!.HasProductAccess(PlatformProduct.SpeedReading).Should().BeTrue();
        savedUser.HasProductAccess(PlatformProduct.Coaching).Should().BeFalse();
        savedUser.Roles.Should().HaveCount(2);
        savedUser.Roles.Should().ContainSingle(userRole =>
            userRole.Role.Name == "Teacher" && userRole.Product == PlatformProduct.Coaching);
        savedUser.Roles.Should().ContainSingle(userRole =>
            userRole.Role.Name == "Teacher" && userRole.Product == PlatformProduct.SpeedReading);
    }

    private sealed class SystemAdministratorCurrentUserService : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public string? Email => "admin@example.test";
        public string? FullName => "System Admin";
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }

    public class UserRepositoryProxy : DispatchProxy
    {
        public User User { get; set; } = null!;
        public IdentityDbContext Context { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IUserRepository.GetByIdAsync):
                    return Task.FromResult<User?>(User);
                case nameof(IUserRepository.RevokeActiveRefreshTokensAsync):
                    return Task.CompletedTask;
                case nameof(IUserRepository.RevokeActiveRefreshTokensForProductAsync):
                    return Task.CompletedTask;
                case nameof(IUserRepository.TrackProductAccessIfNew):
                    var product = (PlatformProduct)args![1]!;
                    var access = User.ProductAccesses.Single(candidate => candidate.Product == product);
                    Context.UserProductAccesses.Add(access);
                    return null;
                default:
                    throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}");
            }
        }
    }

    public class RoleRepositoryProxy : DispatchProxy
    {
        public Role Role { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IRoleRepository.GetByNameAsync) => Task.FromResult<Role?>(Role),
            _ => throw new NotSupportedException($"Unexpected repository call: {targetMethod?.Name}")
        };
    }

    private sealed class CapturingLogger : ILogger<LocalIdentityService>
    {
        public Exception? Exception { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Exception = exception;
    }
}
