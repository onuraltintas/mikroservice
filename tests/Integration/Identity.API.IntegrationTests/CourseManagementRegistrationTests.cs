using System.Reflection;
using EduPlatform.Shared.Contracts.Events.CourseManagement;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Services;
using Identity.Application.Commands.RegisterCourseManagement;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

/// <summary>maxkampus.com registration (19.11.2026): new accounts, existing eduivme accounts and the form's rules.</summary>
public sealed class CourseManagementRegistrationTests
{
    private const string Password = "Guclu-Sifre1";

    [Theory]
    [InlineData("0532 123 45 67", "+905321234567")]
    [InlineData("+90 (212) 555-12-34", "+902125551234")]
    [InlineData("5321234567", "+905321234567")]
    [InlineData("905321234567", "+905321234567")]
    [InlineData("+49 30 1234567", "+49301234567")]
    [InlineData("0123 456 78 90", null)]
    [InlineData("12345", null)]
    [InlineData("0532-123-45-67 dahili 2", null)]
    [InlineData("", null)]
    public void Phone_numbers_are_stored_in_one_form(string written, string? stored) =>
        Assert.Equal(stored, CourseManagementRegistration.NormalizePhone(written));

    [Fact]
    public void The_form_needs_a_kind_a_phone_and_an_institution_name()
    {
        var validator = new RegisterCourseManagementCommandValidator();
        var errors = validator.Validate(new RegisterCourseManagementCommand("school", "test@example.test", Password, "TEST", "Kişi", "123", null))
            .Errors.Select(error => error.PropertyName).ToHashSet();
        Assert.Contains("Kind", errors);
        Assert.Contains("Phone", errors);
        Assert.Contains("WorkspaceName", errors);
        // A teacher who gives private lessons works under their own name when none is given.
        Assert.True(validator.Validate(new RegisterCourseManagementCommand("teacher", "test@example.test", Password, "TEST", "Kişi", "05321234567", null)).IsValid);
    }

    [Fact]
    public async Task A_new_institution_gets_the_owner_role_its_phone_and_a_workspace_request()
    {
        await using var db = Db();
        var userId = Guid.NewGuid();
        var setup = Setup(db, created: User.Create(userId, "test-kurum@example.test", "TEST", "Kişi"));

        var result = await setup.Handler.Handle(new RegisterCourseManagementCommand("institution", "test-kurum@example.test", Password,
            "TEST", "Kişi", "0532 123 45 67", " TEST Dershanesi "), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Code : null);
        var user = setup.Users.Created!;
        Assert.True(user.HasProductAccess(PlatformProduct.CourseManagement));
        Assert.Equal("+905321234567", user.PhoneNumber);
        var role = Assert.Single(await db.Roles.Where(item => item.Name == "CourseInstitutionOwner").ToListAsync());
        Assert.Contains(user.Roles, item => item.RoleId == role.Id && item.Product == PlatformProduct.CourseManagement);
        var workspace = Assert.Single(setup.Publisher.Messages.OfType<CourseManagementWorkspaceRequestedV1>());
        Assert.Equal((userId, "TEST Dershanesi", "institution", CourseManagementRegistration.WorkspaceIdFor(userId)),
            (workspace.UserId, workspace.WorkspaceName, workspace.Kind, workspace.WorkspaceId));
        var verification = Assert.Single(setup.Publisher.Messages.OfType<UserRegisteredEvent>());
        Assert.Equal(("course-management", "CourseInstitutionOwner"), (verification.Product, verification.Role));
    }

    [Fact]
    public async Task A_weak_password_opens_no_new_account()
    {
        await using var db = Db();
        var setup = Setup(db, created: User.Create(Guid.NewGuid(), "test@example.test"));
        var result = await setup.Handler.Handle(new RegisterCourseManagementCommand("teacher", "test@example.test", "zayif",
            "TEST", "Kişi", "05321234567", null), CancellationToken.None);
        Assert.Equal("Validation.Password", result.Error.Code);
        Assert.Equal(0, setup.Identity.RegisterCount);
    }

    [Fact]
    public async Task An_eduivme_account_joins_with_its_own_password_and_keeps_its_other_products()
    {
        await using var db = Db();
        var existing = User.Create(Guid.NewGuid(), "test-var@example.test", "TEST", "Öğretmen");
        var hasher = new PasswordHasher();
        hasher.CreatePasswordHash("Eski-Sifre1", out var hash, out var salt);
        existing.SetPassword(hash, salt);
        existing.ConfirmEmail();
        existing.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        var setup = Setup(db, existing: existing);

        var wrong = await setup.Handler.Handle(new RegisterCourseManagementCommand("teacher", "test-var@example.test", Password,
            "TEST", "Öğretmen", "05321234567", null), CancellationToken.None);
        Assert.Equal("Auth.ExistingAccountPasswordRequired", wrong.Error.Code);
        Assert.Empty(setup.Publisher.Messages);

        var joined = await setup.Handler.Handle(new RegisterCourseManagementCommand("teacher", "test-var@example.test", "Eski-Sifre1",
            "TEST", "Öğretmen", "05321234567", null), CancellationToken.None);
        Assert.True(joined.IsSuccess, joined.IsFailure ? joined.Error.Code : null);
        Assert.Equal("notRequired", joined.Value.VerificationEmail);
        Assert.Equal(0, setup.Identity.RegisterCount);
        Assert.True(existing.HasProductAccess(PlatformProduct.Coaching));
        Assert.True(existing.HasProductAccess(PlatformProduct.CourseManagement));
        var workspace = Assert.Single(setup.Publisher.Messages.OfType<CourseManagementWorkspaceRequestedV1>());
        Assert.Equal(("TEST Öğretmen", "teacher"), (workspace.WorkspaceName, workspace.Kind));
        Assert.Empty(setup.Publisher.Messages.OfType<UserRegisteredEvent>());

        var again = await setup.Handler.Handle(new RegisterCourseManagementCommand("teacher", "test-var@example.test", "Eski-Sifre1",
            "TEST", "Öğretmen", "05321234567", null), CancellationToken.None);
        Assert.Equal("Identity.UserExists", again.Error.Code);
    }

    private static IdentityDbContext Db() =>
        new(new DbContextOptionsBuilder<IdentityDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed record Harness(RegisterCourseManagementCommandHandler Handler, UsersProxy Users, IdentityProxy Identity, PublisherProxy Publisher);

    private static Harness Setup(IdentityDbContext db, User? created = null, User? existing = null)
    {
        var users = DispatchProxy.Create<IUserRepository, UsersProxy>();
        var usersProxy = (UsersProxy)(object)users;
        usersProxy.Created = created;
        usersProxy.Existing = existing;
        var identity = DispatchProxy.Create<IIdentityService, IdentityProxy>();
        var identityProxy = (IdentityProxy)(object)identity;
        identityProxy.UserId = created?.Id ?? Guid.Empty;
        var roles = DispatchProxy.Create<IRoleRepository, CourseManagementJoinTests.CourseRoleRepository>();
        ((CourseManagementJoinTests.CourseRoleRepository)(object)roles).Context = db;
        var publisher = DispatchProxy.Create<IPublishEndpoint, PublisherProxy>();
        var handler = new RegisterCourseManagementCommandHandler(identity, users, roles, new Identity.Infrastructure.Repositories.UnitOfWork(db),
            publisher, DispatchProxy.Create<IConfigurationService, ProductRegistrationProfileOwnershipTests.ConfigurationServiceProxy>(),
            new AcceptingConsent(), new PasswordHasher(), null!, TimeProvider.System);
        return new Harness(handler, usersProxy, identityProxy, (PublisherProxy)(object)publisher);
    }

    public class UsersProxy : DispatchProxy
    {
        public User? Created { get; set; }
        public User? Existing { get; set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IUserRepository.GetByEmailAsync) => Task.FromResult(Existing),
            nameof(IUserRepository.GetByIdAsync) => Task.FromResult(Created ?? Existing),
            nameof(IUserRepository.TrackProductAccessIfNew) => null,
            _ => throw new NotSupportedException(method?.Name)
        };
    }

    public class IdentityProxy : DispatchProxy
    {
        public Guid UserId { get; set; }
        public int RegisterCount { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            switch (method?.Name)
            {
                case nameof(IIdentityService.RegisterUserAsync):
                    RegisterCount++;
                    return Task.FromResult(Result.Success(UserId));
                case nameof(IIdentityService.DeleteUserAsync):
                    return Task.FromResult(Result.Success());
                default:
                    throw new NotSupportedException(method?.Name);
            }
        }
    }

    public class PublisherProxy : DispatchProxy
    {
        public List<object> Messages { get; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (args?[0] is { } message) Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class AcceptingConsent : Identity.Application.LegalPages.IRegistrationLegalConsentService
    {
        public Task<Result<IReadOnlyList<Identity.Application.LegalPages.PlatformLegalPageDto>>> ValidateAsync(PlatformProduct product,
            IEnumerable<Identity.Application.LegalPages.LegalPageAcceptance>? acceptances, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<Identity.Application.LegalPages.PlatformLegalPageDto>>([]));

        public void TrackAcceptedDocuments(Guid userId, PlatformProduct product, IReadOnlyList<Identity.Application.LegalPages.PlatformLegalPageDto> documents,
            string registrationMethod) { }
    }
}
