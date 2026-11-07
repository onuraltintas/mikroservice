using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.GoogleLogin;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.RefreshToken;
using Identity.Domain.Entities;
using PlatformProduct = Identity.Domain.Enums.PlatformProduct;
using UserProductAccessSource = Identity.Domain.Enums.UserProductAccessSource;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Paket F F4 (live 07.11.2026): an admin removed a user's Koçluk Student role; the access row stayed active and the
/// user got a role-less Koçluk token that opened the management shell. Every product session (password, Google,
/// refresh) needs a live role in the product, or SystemAdmin. The management panel (eduivme.com) signs in through
/// the Koçluk scope, so its staff roles (InstitutionAdmin, Editor) are covered by the Coaching cases.
/// </summary>
public sealed partial class SystemAdminMfaLoginTests
{
    private const string NoRoleForProductCode = "Auth.NoRoleForProduct";

    public static TheoryData<PlatformProduct> SignInProducts => new()
    {
        PlatformProduct.Coaching,
        PlatformProduct.SpeedReading
    };

    public static TheoryData<PlatformProduct, string> ProductRoles => new()
    {
        { PlatformProduct.Coaching, "Student" },
        { PlatformProduct.Coaching, "Teacher" },
        { PlatformProduct.Coaching, "Parent" },
        // Management panel (eduivme.com) staff sign in with the Koçluk scope.
        { PlatformProduct.Coaching, "InstitutionAdmin" },
        { PlatformProduct.Coaching, "Editor" },
        { PlatformProduct.SpeedReading, "Student" },
        { PlatformProduct.SpeedReading, "Teacher" }
    };

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task PasswordLogin_WithProductAccessButNoRoleInProduct_IsDenied(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        var tokenService = new IssuingTokenService();

        var result = await CreatePasswordHandler(user, tokenService).Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task PasswordLogin_WhenTheOnlyRoleInProductIsDeleted_IsDenied(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        var deleted = Role.Create("RetiredRole", "Retired role");
        deleted.MarkAsDeleted();
        var membership = new UserRole(user.Id, deleted.Id, product);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(membership, deleted);
        user.AddRole(membership);
        var tokenService = new IssuingTokenService();

        var result = await CreatePasswordHandler(user, tokenService).Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ProductRoles))]
    public async Task PasswordLogin_WithRoleInProduct_IsAllowed(PlatformProduct product, string roleName)
    {
        var user = CreateUserWithRole(roleName, product);
        user.ConfirmEmail();
        user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var tokenService = new IssuingTokenService();

        var result = await CreatePasswordHandler(user, tokenService).Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task PasswordLogin_SystemAdminWithoutProductRole_IsAllowed(PlatformProduct product)
    {
        var user = CreateSystemAdministrator();
        var tokenService = new IssuingTokenService();

        var result = await CreatePasswordHandler(user, tokenService).Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task GoogleLogin_WithProductAccessButNoRoleInProduct_IsDenied(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new IssuingTokenService();

        var result = await CreateGoogleHandler(user, tokenService, allowRefreshToken: true).Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", product),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ProductRoles))]
    public async Task GoogleLogin_WithRoleInProduct_IsAllowed(PlatformProduct product, string roleName)
    {
        var user = CreateUserWithRole(roleName, product);
        user.ConfirmEmail();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var tokenService = new IssuingTokenService();

        var result = await CreateGoogleHandler(user, tokenService, allowRefreshToken: true).Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", product),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task GoogleLogin_SystemAdminWithoutProductRole_IsAllowed(PlatformProduct product)
    {
        var user = CreateSystemAdministrator();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new IssuingTokenService();

        var result = await CreateGoogleHandler(user, tokenService, allowRefreshToken: true).Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1", product),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task RefreshToken_WhenAccountHasNoRoleInSessionProduct_IsDeniedAndRevoked(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        var refreshToken = AddSessionToken(user, product);
        var tokenService = new IssuingTokenService();
        var repository = new StubUserRepository(user);

        var result = await CreateRefreshHandler(repository, tokenService).Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
        refreshToken.IsRevoked.Should().BeTrue();
        repository.RotationCalls.Should().Be(0);
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(ProductRoles))]
    public async Task RefreshToken_WithRoleInSessionProduct_IsRotated(PlatformProduct product, string roleName)
    {
        var user = CreateUserWithRole(roleName, product);
        user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var refreshToken = AddSessionToken(user, product);
        var tokenService = new IssuingTokenService();

        var result = await CreateRefreshHandler(new StubUserRepository(user), tokenService).Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task RefreshToken_SystemAdminWithoutProductRole_IsRotated(PlatformProduct product)
    {
        var user = CreateSystemAdministrator();
        var refreshToken = AddSessionToken(user, product);
        var tokenService = new IssuingTokenService();

        var result = await CreateRefreshHandler(new StubUserRepository(user), tokenService).Handle(
            new RefreshTokenCommand(refreshToken.IssuedValue!),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    // PR #125 review: a wrong password on a role-less account says nothing about its roles.
    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task PasswordLogin_WrongPasswordOnRoleLessAccount_ReturnsInvalidCredentials(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        var tokenService = new IssuingTokenService();
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new RejectingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "wrong-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    // A legacy global (product-less) Student role never reaches a product token, so it does not count.
    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task PasswordLogin_WithOnlyALegacyGlobalStudentRole_IsRefused(PlatformProduct product)
    {
        var user = CreateUserWithRole("Student");
        user.ConfirmEmail();
        user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var tokenService = new IssuingTokenService();

        var result = await CreatePasswordHandler(user, tokenService).Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    // L1: a membership whose role could not be loaded fails closed.
    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task PasswordLogin_WhenTheOnlyMembershipHasNoLoadedRole_IsRefused(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        user.AddRole(new UserRole(user.Id, Guid.NewGuid(), product));
        var tokenService = new IssuingTokenService();

        var result = await CreatePasswordHandler(user, tokenService).Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false, Product: product),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
    }

    // Google registration race: the account appears between the lookup and the registration.
    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task GoogleLogin_RegistrationRaceOntoARoleLessAccount_IsRefused(PlatformProduct product)
    {
        var user = CreateRoleLessUser(product);
        var tokenService = new IssuingTokenService();
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user, null, new User?[] { null }),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(
                Result.Failure<Guid>(new EduPlatform.Shared.Kernel.Results.Error("Identity.UserExists", "exists")),
                allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(allowRegistration: "true"),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>(),
            NoInstitutions());

        var result = await handler.Handle(new GoogleLoginCommand("google-token", "127.0.0.1", product), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(NoRoleForProductCode);
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    // An account created in this request gets its default role through the identity service; the in-memory
    // copy may not show it yet, so that path is explicitly exempt from the role check.
    [Theory]
    [MemberData(nameof(SignInProducts))]
    public async Task GoogleLogin_AccountProvisionedInThisRequest_IsAllowed(PlatformProduct product)
    {
        var user = User.Create(Guid.NewGuid(), "new-google-user@example.com");
        var tokenService = new IssuingTokenService();
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user, null, new User?[] { null }),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(Result.Success(user.Id), allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(allowRegistration: "true"),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance,
            new AcceptingLegalConsentService(),
            System.Reflection.DispatchProxy.Create<MassTransit.IPublishEndpoint, ProductRegistrationProfileOwnershipTests.PublishEndpointProxy>(),
            NoInstitutions());

        var result = await handler.Handle(new GoogleLoginCommand("google-token", "127.0.0.1", product), default);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokenService.AccessTokenProduct.Should().Be(product);
    }

    private sealed class RejectingPasswordHasher : IPasswordHasher
    {
        public void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt) => throw new NotSupportedException();
        public bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt) => false;
        public bool NeedsRehash(byte[] storedHash, byte[] storedSalt) => false;
    }

    /// <summary>The live case: a Student role in the other product only, and an active access row in this one.</summary>
    private static User CreateRoleLessUser(PlatformProduct product)
    {
        var other = product == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
        var user = CreateUserWithRole("Student", other);
        user.ConfirmEmail();
        user.GrantProductAccess(other, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        user.GrantProductAccess(product, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        return user;
    }

    private static RefreshToken AddSessionToken(User user, PlatformProduct product)
    {
        var refreshToken = RefreshToken.Create(
            user.Id,
            $"session-{product}-{Guid.NewGuid():N}",
            DateTime.UtcNow.AddDays(1),
            "127.0.0.1",
            product: product);
        user.AddRefreshToken(refreshToken);
        return refreshToken;
    }

    private static LoginCommandHandler CreatePasswordHandler(User user, IssuingTokenService tokenService) => new(
        new StubUserRepository(user),
        new AcceptingPasswordHasher(),
        tokenService,
        new StubUnitOfWork(),
        new RejectingIdentityService(allowRefreshToken: true),
        new StubConfigurationService(),
        new StubMfaService(user.Id),
        NullLogger<LoginCommandHandler>.Instance);

    private static RefreshTokenCommandHandler CreateRefreshHandler(
        StubUserRepository repository,
        IssuingTokenService tokenService) => new(
        repository,
        tokenService,
        new StubConfigurationService(),
        NullLogger<RefreshTokenCommandHandler>.Instance);
}
