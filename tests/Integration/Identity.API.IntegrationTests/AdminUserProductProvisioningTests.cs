using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Commands.CreateUser;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class AdminUserProductProvisioningTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching, "Student", 1, 0)]
    [InlineData(PlatformProduct.Coaching, "Teacher", 0, 1)]
    [InlineData(PlatformProduct.Coaching, "InstitutionAdmin", 0, 0)]
    [InlineData(PlatformProduct.Coaching, "InstitutionOwner", 0, 0)]
    [InlineData(PlatformProduct.SpeedReading, "Student", 0, 0)]
    [InlineData(PlatformProduct.SpeedReading, "Teacher", 0, 0)]
    [InlineData(PlatformProduct.SpeedReading, "InstitutionAdmin", 0, 0)]
    [InlineData(PlatformProduct.SpeedReading, "InstitutionOwner", 0, 0)]
    public async Task Selected_product_scopes_provisioning_access_profile_and_event(PlatformProduct product, string role, int students, int teachers)
    {
        var fixture = new Fixture();
        var result = await fixture.Handler.Handle(Command(role, product), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(product, fixture.Identity.Product);
        Assert.Equal(role, fixture.Identity.Role);
        Assert.True(fixture.Users.User.HasProductAccess(product));
        Assert.False(fixture.Users.User.HasProductAccess(product == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching));
        Assert.Equal(students, fixture.Students.AddCount);
        Assert.Equal(teachers, fixture.Teachers.AddCount);
        Assert.Equal(product.ToRouteValue(), fixture.Publisher.Event!.Product);
        Assert.Equal(role, fixture.Publisher.Event.Role);
    }

    [Theory]
    [InlineData(null)]
    [InlineData((PlatformProduct)0)]
    [InlineData(PlatformProduct.CourseManagement)]
    public async Task Invalid_product_does_not_create_an_account(PlatformProduct? product)
    {
        var fixture = new Fixture();
        var command = Command("Student", product);
        Assert.False((await new CreateUserCommandValidator().ValidateAsync(command)).IsValid);
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsFailure);
        Assert.Equal(0, fixture.Identity.RegisterCount);
        Assert.Null(fixture.Publisher.Event);
    }

    [Fact]
    public async Task Legacy_constructor_and_json_without_product_keep_coaching_default()
    {
        var fixture = new Fixture();
        var command = System.Text.Json.JsonSerializer.Deserialize<CreateUserCommand>("{\"FirstName\":\"Test\",\"LastName\":\"User\",\"Email\":\"user@example.test\",\"Role\":\"Student\"}")!;
        Assert.Equal(PlatformProduct.Coaching, command.Product);
        Assert.NotNull(typeof(CreateUserCommand).GetConstructor([typeof(string), typeof(string), typeof(string), typeof(string), typeof(string)]));
        Assert.NotNull(typeof(UserCreatedEvent).GetConstructor([typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(DateTime), typeof(DateTime)]));
        Assert.True((await fixture.Handler.Handle(command, CancellationToken.None)).IsSuccess);
        Assert.Equal("coaching", fixture.Publisher.Event!.Product);
    }

    [Fact]
    public async Task SystemAdmin_is_global_even_with_a_previous_product_selection()
    {
        var fixture = new Fixture();
        Assert.True((await fixture.Handler.Handle(Command("SystemAdmin", PlatformProduct.SpeedReading), CancellationToken.None)).IsSuccess);
        Assert.Null(fixture.Identity.Product);
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.SpeedReading));
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Null(fixture.Publisher.Event!.Product);
    }

    [Fact]
    public async Task Existing_email_is_not_overwritten_or_given_a_new_service()
    {
        var fixture = new Fixture();
        fixture.Users.Existing = true;
        var result = await fixture.Handler.Handle(Command("Teacher", PlatformProduct.SpeedReading), CancellationToken.None);
        Assert.Equal("User.Exists", result.Error.Code);
        Assert.Equal(0, fixture.Identity.RegisterCount);
        Assert.False(fixture.Users.User.HasProductAccess(PlatformProduct.SpeedReading));
    }

    [Fact]
    public async Task Failed_profile_or_event_provisioning_invokes_existing_new_account_cleanup()
    {
        var fixture = new Fixture();
        fixture.Publisher.Fail = true;
        Assert.True((await fixture.Handler.Handle(Command("Student", PlatformProduct.Coaching), CancellationToken.None)).IsFailure);
        Assert.Equal(1, fixture.Identity.DeleteCount);
    }

    private static CreateUserCommand Command(string role, PlatformProduct? product) =>
        new("Test", "User", "user@example.test", null, role) { Product = product };

    private sealed class Fixture
    {
        public IdentityProxy Identity { get; }
        public UsersProxy Users { get; }
        public ProfileProxy Students { get; }
        public ProfileProxy Teachers { get; }
        public PublisherProxy Publisher { get; }
        public CreateUserCommandHandler Handler { get; }
        public Fixture()
        {
            var identity = DispatchProxy.Create<IIdentityService, IdentityProxy>();
            Identity = (IdentityProxy)(object)identity;
            var users = DispatchProxy.Create<IUserRepository, UsersProxy>();
            Users = (UsersProxy)(object)users;
            Identity.UserId = Users.User.Id;
            var students = DispatchProxy.Create<IStudentRepository, ProfileProxy>();
            Students = (ProfileProxy)(object)students;
            var teachers = DispatchProxy.Create<ITeacherRepository, ProfileProxy>();
            Teachers = (ProfileProxy)(object)teachers;
            var publisher = DispatchProxy.Create<IPublishEndpoint, PublisherProxy>();
            Publisher = (PublisherProxy)(object)publisher;
            Handler = new CreateUserCommandHandler(identity, users, students, teachers, null!,
                DispatchProxy.Create<IUnitOfWork, UnitOfWorkProxy>(), publisher, new AdminUser(), NullLogger<CreateUserCommandHandler>.Instance);
        }
    }
    public class IdentityProxy : DispatchProxy
    {
        public Guid UserId { get; set; }
        public PlatformProduct? Product { get; private set; }
        public string? Role { get; private set; }
        public int RegisterCount { get; private set; }
        public int DeleteCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name == nameof(IIdentityService.RegisterUserWithRoleAsync))
            {
                RegisterCount++; Role = (string)args![3]!; Product = (PlatformProduct?)args[4];
                return Task.FromResult(Result.Success(new ProvisionedUser(UserId, "one-time-setup-token", DateTime.UtcNow.AddHours(1))));
            }
            if (method.Name == nameof(IIdentityService.DeleteUserAsync)) { DeleteCount++; return Task.FromResult(Result.Success()); }
            throw new NotSupportedException(method.Name);
        }
    }
    public class UsersProxy : DispatchProxy
    {
        public User User { get; } = User.Create(Guid.NewGuid(), "user@example.test", "Test", "User");
        public bool Existing { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
        {
            nameof(IUserRepository.GetByEmailAsync) => Task.FromResult<User?>(Existing ? User : null),
            nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(User),
            nameof(IUserRepository.TrackProductAccessIfNew) => null,
            _ => throw new NotSupportedException(method.Name)
        };
    }
    public class ProfileProxy : DispatchProxy
    {
        public int AddCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method!.Name != "AddAsync") throw new NotSupportedException(method.Name);
            AddCount++; return Task.CompletedTask;
        }
    }
    public class PublisherProxy : DispatchProxy
    {
        public UserCreatedEvent? Event { get; private set; }
        public bool Fail { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (Fail) throw new InvalidOperationException("Test publication failure");
            Event = Assert.IsType<UserCreatedEvent>(args![0]); return Task.CompletedTask;
        }
    }
    public class UnitOfWorkProxy : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult(1);
    }
    private sealed class AdminUser : ICurrentUserService
    {
        public Guid? UserId => Guid.Parse("1d973bab-86d7-4709-a4ec-712f5cafba69");
        public string? Email => "admin@example.test";
        public string? FullName => "Test Admin";
        public IEnumerable<string> Roles => ["SystemAdmin"];
        public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => null;
    }
}