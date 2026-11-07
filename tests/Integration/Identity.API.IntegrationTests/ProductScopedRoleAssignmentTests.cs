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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssignRoleForProduct_ShouldRespectExplicitlyRevokedAccess(bool accessWasRevoked)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var userId = Guid.NewGuid();
        var role = Role.Create("Teacher", "Teacher");
        var user = User.Create(userId, "teacher@example.test");
        user.AddRole(new Identity.Domain.Entities.UserRole(userId, role.Id, PlatformProduct.Coaching));
        if (accessWasRevoked)
        {
            user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
            user.RevokeProductAccess(PlatformProduct.SpeedReading, DateTimeOffset.UtcNow);
        }

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
        savedUser!.HasProductAccess(PlatformProduct.SpeedReading).Should().Be(!accessWasRevoked);
        savedUser.HasProductAccess(PlatformProduct.Coaching).Should().BeFalse();
        savedUser.Roles.Should().HaveCount(2);
        savedUser.Roles.Should().ContainSingle(userRole =>
            userRole.Role.Name == "Teacher" && userRole.Product == PlatformProduct.Coaching);
        savedUser.Roles.Should().ContainSingle(userRole =>
            userRole.Role.Name == "Teacher" && userRole.Product == PlatformProduct.SpeedReading);
    }

    /// <summary>
    /// Paket F F4 (user decision 07.11.2026, PR #125 H1): removing a user's last role in a product also closes that
    /// product's access in the same save, and its sessions are revoked. Only an admin reopens it (Kullanıcı Düzenle).
    /// </summary>
    [Fact]
    public async Task RemovingTheLastRoleInAProductClosesItsAccessAndRevokesItsSessions()
    {
        var harness = await RoleHarness.CreateAsync(
            (PlatformProduct.Coaching, "Student"), (PlatformProduct.SpeedReading, "Student"));

        var result = await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(harness.Failure(result));
        harness.Repository.RevokedSessionProducts.Should().Equal(PlatformProduct.Coaching);
        var saved = await harness.ReloadAsync();
        saved.HasProductAccess(PlatformProduct.Coaching).Should().BeFalse();
        saved.ProductAccesses.Should().ContainSingle(access => access.Product == PlatformProduct.Coaching);
        saved.HasProductAccess(PlatformProduct.SpeedReading).Should().BeTrue();
        saved.GetRolesForProductScope(PlatformProduct.SpeedReading).Should().ContainSingle();
    }

    [Fact]
    public async Task RemovingANonLastRoleInAProductKeepsItsAccess()
    {
        var harness = await RoleHarness.CreateAsync(
            (PlatformProduct.Coaching, "Student"), (PlatformProduct.Coaching, "Teacher"));

        var result = await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(harness.Failure(result));
        var saved = await harness.ReloadAsync();
        saved.HasProductAccess(PlatformProduct.Coaching).Should().BeTrue();
        saved.GetRolesForProductScope(PlatformProduct.Coaching).Should().ContainSingle(role => role.Role.Name == "Teacher");
    }

    [Fact]
    public async Task AnAdminReopeningAccessAndAddingTheRoleLetsTheUserBackIn()
    {
        var harness = await RoleHarness.CreateAsync((PlatformProduct.Coaching, "Student"));
        (await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await harness.ReloadAsync()).HasProductAccess(PlatformProduct.Coaching).Should().BeFalse();

        // Kullanıcı Düzenle reopens the access; Rol Yönetimi adds the role (adding alone does not reopen it).
        var reopened = await new UserAccessManagementService(harness.Context).SetProductAccessAsync(
            harness.UserId, PlatformProduct.Coaching, true, Guid.NewGuid(), null, CancellationToken.None);
        reopened.IsSuccess.Should().BeTrue();
        (await harness.Service.AssignRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var saved = await harness.ReloadAsync();
        saved.HasProductAccess(PlatformProduct.Coaching).Should().BeTrue();
        Identity.Application.Authorization.ProductSignInPolicy
            .HasLiveRoleInProduct(saved, PlatformProduct.Coaching).Should().BeTrue();
    }

    // PR #125 re-review M-1: the last-live-role check is serialised with the per-user lock.
    [Fact]
    public async Task TwoSequentialRemovalsOfTheLastTwoRolesCloseTheAccess()
    {
        var harness = await RoleHarness.CreateAsync(
            (PlatformProduct.Coaching, "Student"), (PlatformProduct.Coaching, "Teacher"));

        (await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await harness.ReloadAsync()).HasProductAccess(PlatformProduct.Coaching).Should().BeTrue();
        (await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Teacher", PlatformProduct.Coaching, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var saved = await harness.ReloadAsync();
        saved.HasProductAccess(PlatformProduct.Coaching).Should().BeFalse();
        saved.GetRolesForProductScope(PlatformProduct.Coaching).Should().BeEmpty();
        harness.Lock.Events.Should().Equal(
            $"acquire:{harness.UserId}", "load", "release",
            $"acquire:{harness.UserId}", "load", "release");
    }

    [Fact]
    public async Task AssignThenRemoveUnderTheLockLeavesAConsistentState()
    {
        var harness = await RoleHarness.CreateAsync((PlatformProduct.Coaching, "Student"));
        harness.Roles["Teacher"] = Role.Create("Teacher", "Teacher");
        harness.Context.Roles.Add(harness.Roles["Teacher"]);
        await harness.Context.SaveChangesAsync();

        (await harness.Service.AssignRoleForProductAsync(
            harness.UserId, "Teacher", PlatformProduct.Coaching, CancellationToken.None)).IsSuccess.Should().BeTrue();
        (await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None)).IsSuccess.Should().BeTrue();

        var saved = await harness.ReloadAsync();
        saved.HasProductAccess(PlatformProduct.Coaching).Should().BeTrue();
        saved.GetRolesForProductScope(PlatformProduct.Coaching).Should().ContainSingle(role => role.Role.Name == "Teacher");
        // Each change loads the user only after the lock is held and releases it afterwards; no overlap.
        harness.Lock.Events.Should().Equal(
            $"acquire:{harness.UserId}", "load", "release",
            $"acquire:{harness.UserId}", "load", "release");
    }

    // L-c: closing the access needs a membership actually removed in this call.
    [Fact]
    public async Task RemovingARoleTheUserDoesNotHaveLeavesTheAccessAlone()
    {
        var harness = await RoleHarness.CreateAsync((PlatformProduct.SpeedReading, "Student"));
        var tracked = await harness.Context.Users.Include(candidate => candidate.ProductAccesses)
            .SingleAsync(candidate => candidate.Id == harness.UserId);
        tracked.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        harness.Context.UserProductAccesses.Add(tracked.ProductAccesses.Single(access => access.Product == PlatformProduct.Coaching));
        await harness.Context.SaveChangesAsync();

        await harness.Service.RemoveRoleForProductAsync(
            harness.UserId, "Student", PlatformProduct.Coaching, CancellationToken.None);

        (await harness.ReloadAsync()).HasProductAccess(PlatformProduct.Coaching).Should().BeTrue();
    }

    [Fact]
    public async Task ReopeningAccessTakesTheSameUserLock()
    {
        var harness = await RoleHarness.CreateAsync((PlatformProduct.Coaching, "Student"));
        var service = new UserAccessManagementService(harness.Context, harness.Lock);

        (await service.SetProductAccessAsync(
            harness.UserId, PlatformProduct.Coaching, false, Guid.NewGuid(), null, CancellationToken.None)).IsSuccess.Should().BeTrue();

        harness.Lock.Events.Should().Equal($"acquire:{harness.UserId}", "release");
    }

    // L-a: a soft-deleted membership is not one of the roles a bulk replacement removes.
    [Fact]
    public async Task BulkRoleReplacementIgnoresSoftDeletedMemberships()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "bulk-stale@example.test");
        var retired = Role.Create("RetiredRole", "RetiredRole");
        retired.MarkAsDeleted();
        var membership = new Identity.Domain.Entities.UserRole(userId, retired.Id, PlatformProduct.Coaching);
        typeof(Identity.Domain.Entities.UserRole).GetProperty(nameof(Identity.Domain.Entities.UserRole.Role))!.SetValue(membership, retired);
        user.AddRole(membership);
        var calls = new List<string>();
        var identity = DispatchProxy.Create<IIdentityService, CallProxy>();
        ((CallProxy)(object)identity).Call = (method, args) => method.Name switch
        {
            nameof(IIdentityService.GetAvailableRolesAsync) =>
                Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success<IEnumerable<string>>(["Teacher"])),
            nameof(IIdentityService.AssignRoleForProductAsync) => Record($"assign:{args![1]}"),
            nameof(IIdentityService.RemoveRoleForProductAsync) => Record($"remove:{args![1]}"),
            _ => throw new NotSupportedException(method.Name)
        };
        var users = DispatchProxy.Create<IUserRepository, CallProxy>();
        ((CallProxy)(object)users).Call = (method, _) => method.Name == nameof(IUserRepository.GetByIdAsync)
            ? Task.FromResult<User?>(user)
            : throw new NotSupportedException(method.Name);
        var controller = new Identity.API.Controllers.BulkUsersController(
            DispatchProxy.Create<MediatR.IMediator, CallProxy>(), identity, users,
            NullLogger<Identity.API.Controllers.BulkUsersController>.Instance);

        await controller.AssignRole("coaching",
            new Identity.API.Controllers.BulkUsersController.BulkRoleAssignmentRequest([userId], "Teacher", RemoveExistingRoles: true),
            CancellationToken.None);

        calls.Should().Equal("assign:Teacher");

        Task<EduPlatform.Shared.Kernel.Results.Result> Record(string call)
        {
            calls.Add(call);
            return Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success());
        }
    }

    public sealed class RecordingAuthenticationLock(Action? onLoad = null) : IAuthenticationLock
    {
        public List<string> Events { get; } = [];
        private int _held;

        public Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _held, 1) == 1)
                throw new InvalidOperationException("The user lock was taken twice.");
            Events.Add($"acquire:{userId}");
            return Task.FromResult<IAsyncDisposable>(new Lease(this));
        }

        public void RecordLoad()
        {
            if (Volatile.Read(ref _held) == 1) Events.Add("load");
            else Events.Add("load-outside-lock");
            onLoad?.Invoke();
        }

        private sealed class Lease(RecordingAuthenticationLock owner) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                owner.Events.Add("release");
                Interlocked.Exchange(ref owner._held, 0);
                return ValueTask.CompletedTask;
            }
        }
    }

    // F4: a bulk "replace the service's roles" must never pass through zero roles, or it would close the access
    // (and assigning a role does not reopen an access closed that way). It assigns first, then removes the others.
    [Fact]
    public async Task BulkRoleReplacementAssignsTheNewRoleBeforeRemovingTheOldOnes()
    {
        var userId = Guid.NewGuid();
        var role = Role.Create("Student", "Student");
        var user = User.Create(userId, "bulk@example.test");
        var membership = new Identity.Domain.Entities.UserRole(userId, role.Id, PlatformProduct.Coaching);
        typeof(Identity.Domain.Entities.UserRole).GetProperty(nameof(Identity.Domain.Entities.UserRole.Role))!.SetValue(membership, role);
        user.AddRole(membership);
        var calls = new List<string>();
        var identity = DispatchProxy.Create<IIdentityService, CallProxy>();
        ((CallProxy)(object)identity).Call = (method, args) => method.Name switch
        {
            nameof(IIdentityService.GetAvailableRolesAsync) =>
                Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success<IEnumerable<string>>(["Student", "Teacher"])),
            nameof(IIdentityService.AssignRoleForProductAsync) => Record($"assign:{args![1]}"),
            nameof(IIdentityService.RemoveRoleForProductAsync) => Record($"remove:{args![1]}"),
            _ => throw new NotSupportedException(method.Name)
        };
        var users = DispatchProxy.Create<IUserRepository, CallProxy>();
        ((CallProxy)(object)users).Call = (method, _) => method.Name == nameof(IUserRepository.GetByIdAsync)
            ? Task.FromResult<User?>(user)
            : throw new NotSupportedException(method.Name);
        var controller = new Identity.API.Controllers.BulkUsersController(
            DispatchProxy.Create<MediatR.IMediator, CallProxy>(), identity, users,
            NullLogger<Identity.API.Controllers.BulkUsersController>.Instance);

        await controller.AssignRole("coaching",
            new Identity.API.Controllers.BulkUsersController.BulkRoleAssignmentRequest([userId], "Teacher", RemoveExistingRoles: true),
            CancellationToken.None);

        calls.Should().Equal("assign:Teacher", "remove:Student");

        Task<EduPlatform.Shared.Kernel.Results.Result> Record(string call)
        {
            calls.Add(call);
            return Task.FromResult(EduPlatform.Shared.Kernel.Results.Result.Success());
        }
    }

    public class CallProxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Call { get; set; } = (method, _) => throw new NotSupportedException(method.Name);
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args);
    }

    private sealed class RoleHarness
    {
        public required IdentityDbContext Context { get; init; }
        public required UserRepositoryProxy Repository { get; init; }
        public required LocalIdentityService Service { get; init; }
        public required CapturingLogger Logger { get; init; }
        public required Guid UserId { get; init; }
        public required RecordingAuthenticationLock Lock { get; init; }
        public required Dictionary<string, Role> Roles { get; init; }

        public static async Task<RoleHarness> CreateAsync(params (PlatformProduct Product, string Role)[] memberships)
        {
            var options = new DbContextOptionsBuilder<IdentityDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var userId = Guid.NewGuid();
            var roles = memberships.Select(item => item.Role).Distinct()
                .ToDictionary(name => name, name => Role.Create(name, name));
            var user = User.Create(userId, "member@example.test");
            foreach (var (product, roleName) in memberships)
            {
                user.AddRole(new Identity.Domain.Entities.UserRole(userId, roles[roleName].Id, product));
                user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            }

            var context = new IdentityDbContext(options);
            context.Users.Add(user);
            context.Roles.AddRange(roles.Values);
            await context.SaveChangesAsync();

            var userRepository = DispatchProxy.Create<IUserRepository, UserRepositoryProxy>();
            var proxy = (UserRepositoryProxy)(object)userRepository;
            proxy.User = await context.Users.SingleAsync();
            proxy.Context = context;
            var roleRepository = DispatchProxy.Create<IRoleRepository, RoleRepositoryProxy>();
            ((RoleRepositoryProxy)(object)roleRepository).Roles = roles;
            var logger = new CapturingLogger();
            var authenticationLock = new RecordingAuthenticationLock();
            proxy.OnLoad = authenticationLock.RecordLoad;
            return new RoleHarness
            {
                Lock = authenticationLock,
                Roles = roles,
                Context = context,
                Repository = proxy,
                Logger = logger,
                UserId = userId,
                Service = new LocalIdentityService(
                    userRepository,
                    passwordHasher: null!,
                    new UnitOfWork(context),
                    logger,
                    tokenService: null!,
                    roleRepository,
                    context,
                    new SystemAdministratorCurrentUserService(),
                    authenticationLock)
            };
        }

        public string? Failure(EduPlatform.Shared.Kernel.Results.Result result) =>
            Logger.Exception?.ToString() ?? (result.IsFailure ? result.Error.Description : null);

        public Task<User> ReloadAsync() => Context.Users.Include(candidate => candidate.ProductAccesses)
            .Include(candidate => candidate.Roles).ThenInclude(userRole => userRole.Role)
            .SingleAsync(candidate => candidate.Id == UserId);
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
        public List<PlatformProduct> RevokedSessionProducts { get; } = [];
        public Action? OnLoad { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IUserRepository.GetByIdAsync):
                    OnLoad?.Invoke();
                    return Task.FromResult<User?>(User);
                case nameof(IUserRepository.RevokeActiveRefreshTokensAsync):
                    return Task.CompletedTask;
                case nameof(IUserRepository.RevokeActiveRefreshTokensForProductAsync):
                    RevokedSessionProducts.Add((PlatformProduct)args![1]!);
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
        public Dictionary<string, Role>? Roles { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IRoleRepository.GetByNameAsync) => Task.FromResult<Role?>(
                Roles is null ? Role : Roles.GetValueOrDefault((string)args![0]!)),
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
