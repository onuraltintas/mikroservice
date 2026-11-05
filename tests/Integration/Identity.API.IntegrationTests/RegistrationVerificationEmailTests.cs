using System.Reflection;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Identity;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using Identity.API.Controllers;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Application.Registration;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using MassTransit;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Paket A #2 (05.11.2026): one identity, product access per product. Registering for a
/// product tells the client whether a verification e-mail went out, so the success screen
/// never claims "gönderildi" for an account whose address is already verified.
/// </summary>
public sealed class RegistrationVerificationEmailTests
{
    public static TheoryData<PlatformProduct, string> Directions()
    {
        var data = new TheoryData<PlatformProduct, string>();
        foreach (var target in new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading })
            foreach (var account in new[] { "student", "teacher", "institution" })
                data.Add(target, account);
        return data;
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task VerifiedAccountFromTheOtherProductJoinsWithoutAVerificationEmail(PlatformProduct target, string account)
    {
        var f = new EnrollmentFixture(target, account, confirmed: true);

        var result = await f.Join();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(new RegistrationOutcome(f.User.Id, RegistrationOutcome.NotRequired), result.Value);
        Assert.True(f.User.HasProductAccess(target));
        Assert.True(f.User.EmailConfirmed);
        Assert.Null(f.User.EmailVerificationToken);
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
    }

    [Theory]
    [MemberData(nameof(Directions))]
    public async Task UnverifiedAccountFromTheOtherProductJoinsAndGetsAVerificationEmail(PlatformProduct target, string account)
    {
        var f = new EnrollmentFixture(target, account, confirmed: false);
        var passwordHash = f.User.PasswordHash.ToArray();

        var result = await f.Join();

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(new RegistrationOutcome(f.User.Id, RegistrationOutcome.Sent), result.Value);
        Assert.True(f.User.HasProductAccess(target));
        Assert.False(f.User.EmailConfirmed);
        Assert.Equal(passwordHash, f.User.PasswordHash);
        var mail = Assert.Single(f.Published.OfType<UserRegisteredEvent>());
        Assert.Equal(f.User.Id, mail.UserId);
        Assert.Equal(f.User.Email, mail.Email);
        Assert.False(string.IsNullOrWhiteSpace(mail.VerificationToken));
        Assert.Equal(f.User.EmailVerificationToken, mail.VerificationToken);
        Assert.Equal(account == "student" ? "Student" : account == "teacher" ? "Teacher" : "InstitutionAdmin", mail.Role);
        // Review #2: the e-mail is branded and linked for the product being joined, not inferred from the role.
        Assert.Equal(target.ToRouteValue(), mail.Product);
        Assert.Equal(1, f.Saves);
    }

    [Theory]
    [InlineData(PlatformProduct.Coaching, true)]
    [InlineData(PlatformProduct.Coaching, false)]
    [InlineData(PlatformProduct.SpeedReading, true)]
    [InlineData(PlatformProduct.SpeedReading, false)]
    public async Task JoiningStillRequiresTheExistingPasswordAndSendsNothingWithoutIt(PlatformProduct target, bool confirmed)
    {
        var f = new EnrollmentFixture(target, "student", confirmed);

        var result = await f.Join("WrongPassword1!");

        Assert.Equal("Auth.ExistingAccountPasswordRequired", result.Error.Code);
        Assert.False(f.User.HasProductAccess(target));
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
        Assert.Null(f.User.EmailVerificationToken);
        Assert.Equal(0, f.Saves);
    }

    [Fact]
    public async Task InactiveUnverifiedAccountStillCannotJoin()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);
        f.User.Deactivate();

        var result = await f.Join();

        Assert.Equal("Auth.ExistingAccountNotEligible", result.Error.Code);
        Assert.False(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
    }

    // The registration forms (not only the explicit join form) reach the join path when the
    // e-mail already has an account; both directions, both verification states.
    [Theory]
    [InlineData(PlatformProduct.Coaching, true, RegistrationOutcome.NotRequired)]
    [InlineData(PlatformProduct.Coaching, false, RegistrationOutcome.Sent)]
    [InlineData(PlatformProduct.SpeedReading, true, RegistrationOutcome.NotRequired)]
    [InlineData(PlatformProduct.SpeedReading, false, RegistrationOutcome.Sent)]
    public async Task StudentRegistrationOfAnExistingAccountReportsTheVerificationEmail(PlatformProduct target, bool confirmed, string expected)
    {
        var f = new EnrollmentFixture(target, "student", confirmed);
        var handler = new RegisterStudentCommandHandler(f.ExistingIdentity(), f.Users(), Stub<IStudentRepository>((m, _) => throw new NotSupportedException(m.Name)),
            f.UnitOfWork(), f.Publisher(), Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")), new AcceptingLegal(), f.Enrollment);

        var result = await handler.Handle(new RegisterStudentCommand(f.User.Email, EnrollmentFixture.Password, "Yeni", "Ad", null, target), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(expected, result.Value.VerificationEmail);
        Assert.Equal(f.User.Id, result.Value.UserId);
        Assert.Equal(expected == RegistrationOutcome.Sent ? 1 : 0, f.Published.OfType<UserRegisteredEvent>().Count());
    }

    [Theory]
    [InlineData(PlatformProduct.Coaching, true, RegistrationOutcome.NotRequired)]
    [InlineData(PlatformProduct.Coaching, false, RegistrationOutcome.Sent)]
    [InlineData(PlatformProduct.SpeedReading, true, RegistrationOutcome.NotRequired)]
    [InlineData(PlatformProduct.SpeedReading, false, RegistrationOutcome.Sent)]
    public async Task TeacherRegistrationOfAnExistingAccountReportsTheVerificationEmail(PlatformProduct target, bool confirmed, string expected)
    {
        var f = new EnrollmentFixture(target, "teacher", confirmed);
        var handler = new RegisterTeacherCommandHandler(f.ExistingIdentity(), f.Users(), Stub<ITeacherRepository>((m, _) => throw new NotSupportedException(m.Name)),
            f.UnitOfWork(), f.Publisher(), Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")), new AcceptingLegal(), f.Enrollment);

        var result = await handler.Handle(new RegisterTeacherCommand(f.User.Email, EnrollmentFixture.Password, "Yeni", "Ad", null, target), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(expected, result.Value.VerificationEmail);
        Assert.Equal(expected == RegistrationOutcome.Sent ? 1 : 0, f.Published.OfType<UserRegisteredEvent>().Count());
    }

    // First-time registration: a new account always gets the verification e-mail.
    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task NewAccountRegistrationSendsTheVerificationEmail(PlatformProduct product)
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "new@example.test", "Yeni", "Öğrenci");
        var published = new List<object>();
        var handler = new RegisterStudentCommandHandler(
            Stub<IIdentityService>((m, _) => m.Name switch
            {
                nameof(IIdentityService.RegisterUserAsync) => Task.FromResult(Result.Success(userId)),
                nameof(IIdentityService.AssignRoleForProductAsync) => Task.FromResult(Result.Success()),
                _ => throw new NotSupportedException(m.Name)
            }),
            Stub<IUserRepository>((m, _) => m.Name switch
            {
                nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(user),
                nameof(IUserRepository.TrackProductAccessIfNew) => null,
                _ => throw new NotSupportedException(m.Name)
            }),
            Stub<IStudentRepository>((m, _) => throw new NotSupportedException(m.Name)),
            Stub<IUnitOfWork>((_, _) => Task.FromResult(1)),
            Stub<IPublishEndpoint>((_, args) => { published.Add(args![0]!); return Task.CompletedTask; }),
            Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")),
            new AcceptingLegal(), null!);

        var result = await handler.Handle(new RegisterStudentCommand("new@example.test", "StrongPassword1!", "Yeni", "Öğrenci", null, product), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(new RegistrationOutcome(userId, RegistrationOutcome.Sent), result.Value);
        var mail = Assert.Single(published.OfType<UserRegisteredEvent>());
        Assert.Equal(user.EmailVerificationToken, mail.VerificationToken);
        Assert.Equal(product.ToRouteValue(), mail.Product);
    }

    // The API returns the outcome next to the user id (additive: older clients read only userId).
    [Theory]
    [InlineData(RegistrationOutcome.Sent)]
    [InlineData(RegistrationOutcome.NotRequired)]
    public async Task RegistrationEndpointsReturnTheVerificationEmailOutcome(string outcome)
    {
        var userId = Guid.NewGuid();
        var mediator = DispatchProxy.Create<IMediator, OutcomeMediator>();
        ((OutcomeMediator)(object)mediator).Outcome = new RegistrationOutcome(userId, outcome);
        var controller = new AuthController(mediator, null!);

        var responses = new[]
        {
            await controller.RegisterStudent("coaching", new RegisterStudentCommand("a@example.test", "StrongPassword1!", "A", "B", null)),
            await controller.RegisterTeacher("speed-reading", new RegisterTeacherCommand("a@example.test", "StrongPassword1!", "A", "B", null)),
            await controller.RegisterInstitution("coaching", new Identity.Application.Commands.RegisterInstitution.RegisterInstitutionCommand(
                "a@example.test", "StrongPassword1!", "A", "B", "Okul", InstitutionType.School, null, "34", "3401")),
            await controller.RegisterParent("coaching", new Identity.Application.Commands.RegisterParent.RegisterParentCommand(
                "a@example.test", "StrongPassword1!", "A", "B", null)),
            await controller.JoinProduct("speed-reading", new Identity.Application.Commands.JoinProduct.JoinProductCommand(
                "a@example.test", "StrongPassword1!", "student")),
        };

        foreach (var response in responses)
        {
            var body = Assert.IsType<OkObjectResult>(response).Value!;
            var json = System.Text.Json.JsonSerializer.Serialize(body, body.GetType(), AppJsonOptions());
            Assert.Equal($"{{\"userId\":\"{userId}\",\"verificationEmail\":\"{outcome}\"}}", json);
        }
    }

    // ---- Review follow-up (PR #111) ----

    // Review #1: a coaching parent registration of an existing account joins (any source role) with the same e-mail rule.
    [Theory]
    [InlineData(true, RegistrationOutcome.NotRequired)]
    [InlineData(false, RegistrationOutcome.Sent)]
    public async Task ParentRegistrationOfAnExistingAccountJoinsCoachingAsParent(bool confirmed, string expected)
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "parent", confirmed);
        var result = await ParentHandler(f).Handle(new Identity.Application.Commands.RegisterParent.RegisterParentCommand(
            f.User.Email, EnrollmentFixture.Password, "Yeni", "Veli", null, PlatformProduct.Coaching), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(new RegistrationOutcome(f.User.Id, expected), result.Value);
        Assert.True(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Contains(f.User.Roles, r => r.Product == PlatformProduct.Coaching && r.RoleId == f.Roles["Parent"].Id);
        Assert.DoesNotContain(f.User.Roles, r => r.Product == PlatformProduct.Coaching && r.RoleId == f.Roles["Student"].Id);
        var profile = Assert.Single(f.ParentProfiles);
        Assert.Equal(f.User.Id, profile.UserId);
        Assert.Empty(f.Published.OfType<CoachingProfileProvisioningRequested>());
        var mails = f.Published.OfType<UserRegisteredEvent>().ToList();
        if (expected == RegistrationOutcome.Sent)
        {
            var mail = Assert.Single(mails);
            Assert.Equal("Parent", mail.Role);
            Assert.Equal("coaching", mail.Product);
        }
        else Assert.Empty(mails);
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("institution")]
    public async Task ParentJoinDoesNotRequireAMatchingSourceRole(string sourceAccount)
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, sourceAccount, confirmed: true);
        var result = await f.Enrollment.JoinAsync(f.User.Email, EnrollmentFixture.Password, PlatformProduct.Coaching, "parent", [], null, CancellationToken.None);
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Contains(f.User.Roles, r => r.Product == PlatformProduct.Coaching && r.RoleId == f.Roles["Parent"].Id);
    }

    [Theory]
    [InlineData("wrong-password", "Auth.ExistingAccountPasswordRequired")]
    [InlineData("inactive", "Auth.ExistingAccountNotEligible")]
    [InlineData("mfa", "Auth.ExistingAccountMfaRequired")]
    [InlineData("speed-reading", "Auth.InvalidProduct")]
    public async Task ParentJoinIsRefusedWithoutProof(string scenario, string code)
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "parent", confirmed: true);
        if (scenario == "inactive") f.User.Deactivate();
        if (scenario == "mfa") f.User.EnableMfa("protected", ["recovery"], DateTimeOffset.UtcNow);
        var result = await f.Enrollment.JoinAsync(f.User.Email, scenario == "wrong-password" ? "WrongPassword1!" : EnrollmentFixture.Password,
            scenario == "speed-reading" ? PlatformProduct.SpeedReading : PlatformProduct.Coaching, "parent", [], null, CancellationToken.None);

        Assert.Equal(code, result.Error.Code);
        Assert.False(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.DoesNotContain(f.User.Roles, r => r.RoleId == f.Roles["Parent"].Id);
        Assert.Empty(f.ParentProfiles);
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
    }

    [Fact]
    public async Task ParentRegistrationOfAnExistingAccountWithTheWrongPasswordIsRefused()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "parent", confirmed: false);
        var result = await ParentHandler(f).Handle(new Identity.Application.Commands.RegisterParent.RegisterParentCommand(
            f.User.Email, "WrongPassword1!", "Yeni", "Veli", null, PlatformProduct.Coaching), CancellationToken.None);
        Assert.Equal("Auth.ExistingAccountPasswordRequired", result.Error.Code);
        Assert.False(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
    }

    // Review #2: every new-account registration names its product on the verification e-mail.
    [Theory]
    [InlineData(PlatformProduct.Coaching)]
    [InlineData(PlatformProduct.SpeedReading)]
    public async Task NewTeacherRegistrationNamesTheProductOnTheVerificationEmail(PlatformProduct product)
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "new@example.test", "Yeni", "Öğretmen");
        var published = new List<object>();
        var handler = new RegisterTeacherCommandHandler(NewIdentity(userId), NewUsers(user),
            Stub<ITeacherRepository>((m, _) => throw new NotSupportedException(m.Name)), Stub<IUnitOfWork>((_, _) => Task.FromResult(1)),
            Stub<IPublishEndpoint>((_, args) => { published.Add(args![0]!); return Task.CompletedTask; }),
            Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")), new AcceptingLegal(), null!);

        var result = await handler.Handle(new RegisterTeacherCommand("new@example.test", "StrongPassword1!", "Yeni", "Öğretmen", null, product), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(product.ToRouteValue(), Assert.Single(published.OfType<UserRegisteredEvent>()).Product);
    }

    [Fact]
    public async Task NewParentRegistrationNamesCoachingOnTheVerificationEmail()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "veli@example.test", "Yeni", "Veli");
        var published = new List<object>();
        var handler = new Identity.Application.Commands.RegisterParent.RegisterParentCommandHandler(NewIdentity(userId), NewUsers(user),
            Stub<IParentRepository>((_, _) => Task.CompletedTask), Stub<IUnitOfWork>((_, _) => Task.FromResult(1)),
            Stub<IPublishEndpoint>((_, args) => { published.Add(args![0]!); return Task.CompletedTask; }),
            Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")), new AcceptingLegal(), null!);

        var result = await handler.Handle(new Identity.Application.Commands.RegisterParent.RegisterParentCommand(
            "veli@example.test", "StrongPassword1!", "Yeni", "Veli", null, PlatformProduct.Coaching), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(new RegistrationOutcome(userId, RegistrationOutcome.Sent), result.Value);
        Assert.Equal("coaching", Assert.Single(published.OfType<UserRegisteredEvent>()).Product);
    }

    // Review #8: a retry after a lost response is idempotent instead of "Bu serviste kaydınız zaten var".
    [Theory]
    [InlineData("student")]
    [InlineData("teacher")]
    [InlineData("institution")]
    [InlineData("parent")]
    public async Task ASecondJoinOfAnUnverifiedAccountSendsAFreshLinkAgain(string account)
    {
        var target = account == "parent" ? PlatformProduct.Coaching : PlatformProduct.SpeedReading;
        var f = new EnrollmentFixture(target, account, confirmed: false);
        var first = await f.Join();
        Assert.True(first.IsSuccess, first.IsFailure ? first.Error.Description : "");
        var firstToken = f.User.EmailVerificationToken;

        var second = await f.Join();

        Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Description : "");
        Assert.Equal(new RegistrationOutcome(f.User.Id, RegistrationOutcome.Sent), second.Value);
        Assert.Equal(2, f.Published.OfType<UserRegisteredEvent>().Count());
        Assert.NotEqual(firstToken, f.User.EmailVerificationToken);
        Assert.Equal(f.User.EmailVerificationToken, f.Published.OfType<UserRegisteredEvent>().Last().VerificationToken);
        Assert.Single(f.User.ProductAccesses, a => a.Product == target);
    }

    [Theory]
    [InlineData("student")]
    [InlineData("parent")]
    public async Task ASecondJoinOfAVerifiedAccountSaysSignIn(string account)
    {
        var target = account == "parent" ? PlatformProduct.Coaching : PlatformProduct.SpeedReading;
        var f = new EnrollmentFixture(target, account, confirmed: true);
        Assert.True((await f.Join()).IsSuccess);

        var second = await f.Join();

        Assert.True(second.IsSuccess, second.IsFailure ? second.Error.Description : "");
        Assert.Equal(RegistrationOutcome.NotRequired, second.Value.VerificationEmail);
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
    }

    [Fact]
    public async Task ARetryWithTheWrongPasswordStillFails()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);
        Assert.True((await f.Join()).IsSuccess);
        Assert.Equal("Auth.ExistingAccountPasswordRequired", (await f.Join("WrongPassword1!")).Error.Code);
        Assert.Single(f.Published.OfType<UserRegisteredEvent>());
    }

    [Fact]
    public async Task ARevokedTargetAccessIsStillAConflictEvenWhenUnverified()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);
        f.User.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        f.AddRole("Student", PlatformProduct.Coaching);
        f.User.RevokeProductAccess(PlatformProduct.Coaching, DateTimeOffset.UtcNow);
        Assert.Equal("Identity.UserExists", (await f.Join()).Error.Code);
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
    }

    // Review #9: the link issued before the join stops working; only the new one verifies.
    [Fact]
    public async Task JoiningRotatesTheVerificationToken()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);
        f.User.GenerateEmailVerificationToken();
        var oldToken = f.User.EmailVerificationToken!;

        Assert.True((await f.Join()).IsSuccess);
        var newToken = f.User.EmailVerificationToken!;
        Assert.NotEqual(oldToken, newToken);

        var confirm = new Identity.Application.Commands.ConfirmEmail.ConfirmEmailCommandHandler(f.Users(), f.UnitOfWork(), f.Publisher());
        Assert.Equal("User.InvalidToken", (await confirm.Handle(new(f.User.Id, oldToken), CancellationToken.None)).Error.Code);
        Assert.False(f.User.EmailConfirmed);
        Assert.Equal(newToken, Assert.Single(f.Published.OfType<UserRegisteredEvent>()).VerificationToken);
        Assert.True(f.User.EmailVerificationTokenExpiresAt > DateTime.UtcNow.AddHours(23));
    }

    [Theory]
    [InlineData(true, RegistrationOutcome.NotRequired)]
    [InlineData(false, RegistrationOutcome.Sent)]
    public async Task InstitutionRegistrationOfAnExistingAccountReportsTheVerificationEmail(bool confirmed, string expected)
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "institution", confirmed);
        var handler = new Identity.Application.Commands.RegisterInstitution.RegisterInstitutionCommandHandler(
            Stub<IIdentityService>((m, _) => throw new NotSupportedException(m.Name)), f.Users(),
            Stub<IInstitutionRepository>((m, _) => throw new NotSupportedException(m.Name)), f.UnitOfWork(), f.Publisher(),
            Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")),
            Stub<ILocationRepository>((m, _) => throw new NotSupportedException(m.Name)), new AcceptingLegal(), f.Enrollment);

        var result = await handler.Handle(new Identity.Application.Commands.RegisterInstitution.RegisterInstitutionCommand(
            f.User.Email, EnrollmentFixture.Password, "Yeni", "Ad", "Yeni kurum", InstitutionType.School, null, "34", "3401",
            PlatformProduct.Coaching), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(new RegistrationOutcome(f.User.Id, expected), result.Value);
        Assert.Contains(f.Admins, a => a.Product == PlatformProduct.Coaching && a.InstitutionId == f.Institution.Id);
        Assert.Equal(expected == RegistrationOutcome.Sent ? 1 : 0, f.Published.OfType<UserRegisteredEvent>().Count());
    }

    [Fact]
    public async Task AnUnverifiedAccountWithMfaStillNeedsAFreshCode()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);
        f.User.EnableMfa("protected", ["recovery"], DateTimeOffset.UtcNow);

        Assert.Equal("Auth.ExistingAccountMfaRequired", (await f.Join()).Error.Code);
        Assert.False(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());
        Assert.Equal("Auth.InvalidMfaCode", (await f.Join(mfaCode: "000000")).Error.Code);
        Assert.Empty(f.Published.OfType<UserRegisteredEvent>());

        var result = await f.Join(mfaCode: "123456");
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(RegistrationOutcome.Sent, result.Value.VerificationEmail);
        Assert.Single(f.Published.OfType<UserRegisteredEvent>());
    }

    // Review #3: when the real owner claims an unconfirmed account with a verified Google login,
    // what was joined with the (possibly someone else's) password while unconfirmed is taken back.
    [Fact]
    public async Task ClaimingAnUnconfirmedAccountRevokesWhatWasJoinedWhileUnconfirmed()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "institution", confirmed: false);
        Assert.True((await f.Join()).IsSuccess);
        Assert.True(f.User.HasProductAccess(PlatformProduct.Coaching));
        var coachingLink = Assert.Single(f.Admins, a => a.Product == PlatformProduct.Coaching);

        // Second review L4: a link in the same product that someone else created is not touched.
        var other = Institution.Create("Başka kurum", InstitutionType.School);
        var otherLink = InstitutionAdmin.Create(f.User.Id, other.Id, InstitutionAdminRole.Manager, PlatformProduct.Coaching);
        f.Admins.Add(otherLink);

        var revoked = await UnconfirmedAccountClaim.ClaimAsync(f.User, f.Institutions, CancellationToken.None);

        Assert.Equal(new[] { PlatformProduct.Coaching }, revoked);
        Assert.True(f.User.EmailConfirmed);
        Assert.False(f.User.HasProductAccess(PlatformProduct.Coaching));
        // Second review M1: removed, not revoked, so the owner can join again.
        Assert.DoesNotContain(f.User.ProductAccesses, a => a.Product == PlatformProduct.Coaching);
        Assert.DoesNotContain(f.User.Roles, r => r.Product == PlatformProduct.Coaching);
        Assert.DoesNotContain(coachingLink, f.Admins);
        Assert.Contains(otherLink, f.Admins);
        Assert.True(otherLink.IsActive);
        Assert.True(f.User.HasProductAccess(PlatformProduct.SpeedReading), "the original registration stays (existing behaviour)");
        Assert.Contains(f.User.Roles, r => r.Product == PlatformProduct.SpeedReading);
        Assert.All(f.Admins.Where(a => a.Product == PlatformProduct.SpeedReading), a => Assert.True(a.IsActive));
        Assert.Empty(f.User.PasswordHash);
    }

    [Fact]
    public async Task ClaimingAConfirmedAccountChangesNothing()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: true);
        Assert.True((await f.Join()).IsSuccess);
        var revoked = await UnconfirmedAccountClaim.ClaimAsync(f.User, f.Institutions, CancellationToken.None);
        Assert.Empty(revoked);
        Assert.True(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.NotEmpty(f.User.PasswordHash);
    }

    // Review #7: the persistence failure message is Turkish like the rest of registration.
    [Fact]
    public async Task APersistenceFailureIsReportedInTurkish()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(userId, "new@example.test", "Yeni", "Öğrenci");
        var identity = Stub<IIdentityService>((m, _) => m.Name switch
        {
            nameof(IIdentityService.RegisterUserAsync) => Task.FromResult(Result.Success(userId)),
            nameof(IIdentityService.AssignRoleForProductAsync) => Task.FromResult(Result.Success()),
            nameof(IIdentityService.DeleteUserAsync) => Task.FromResult(Result.Success()),
            _ => throw new NotSupportedException(m.Name)
        });
        var failingSave = Stub<IUnitOfWork>((_, _) => throw new InvalidOperationException("write failed"));
        var publisher = Stub<IPublishEndpoint>((_, _) => Task.CompletedTask);
        var config = Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true"));
        var results = new[]
        {
            await new RegisterStudentCommandHandler(identity, NewUsers(user), Stub<IStudentRepository>((_, _) => null), failingSave, publisher, config, new AcceptingLegal(), null!)
                .Handle(new RegisterStudentCommand("new@example.test", "StrongPassword1!", "Yeni", "Öğrenci", null, PlatformProduct.Coaching), CancellationToken.None),
            await new RegisterTeacherCommandHandler(identity, NewUsers(user), Stub<ITeacherRepository>((_, _) => null), failingSave, publisher, config, new AcceptingLegal(), null!)
                .Handle(new RegisterTeacherCommand("new@example.test", "StrongPassword1!", "Yeni", "Öğretmen", null, PlatformProduct.Coaching), CancellationToken.None),
            await new Identity.Application.Commands.RegisterParent.RegisterParentCommandHandler(identity, NewUsers(user), Stub<IParentRepository>((_, _) => Task.CompletedTask), failingSave, publisher, config, new AcceptingLegal(), null!)
                .Handle(new Identity.Application.Commands.RegisterParent.RegisterParentCommand("new@example.test", "StrongPassword1!", "Yeni", "Veli", null, PlatformProduct.Coaching), CancellationToken.None),
        };
        foreach (var result in results)
        {
            Assert.Equal("Registration.Failed", result.Error.Code);
            Assert.Equal("Kayıt tamamlanamadı. Lütfen biraz sonra yeniden deneyin.", result.Error.Description);
        }
    }

    // ---- Second review (PR #111) ----

    // M1: unverified join -> verified Google claim -> the owner (after setting a password) joins again -> verified flow.
    [Theory]
    [InlineData("student")]
    [InlineData("institution")]
    [InlineData("parent")]
    public async Task TheRealOwnerCanJoinAgainAfterClaimingTheAccount(string account)
    {
        var target = PlatformProduct.Coaching;
        var f = new EnrollmentFixture(target, account, confirmed: false);
        Assert.Equal(RegistrationOutcome.Sent, (await f.Join()).Value.VerificationEmail);

        await UnconfirmedAccountClaim.ClaimAsync(f.User, f.Institutions, CancellationToken.None);
        Assert.DoesNotContain(f.User.ProductAccesses, a => a.Product == target);
        Assert.Empty(f.User.PasswordHash);

        f.SetPassword("OwnersNewPassword1!");
        var again = await f.Join("OwnersNewPassword1!");

        Assert.True(again.IsSuccess, again.IsFailure ? again.Error.Description : "");
        Assert.Equal(RegistrationOutcome.NotRequired, again.Value.VerificationEmail);
        Assert.True(f.User.HasProductAccess(target));
        Assert.Single(f.User.ProductAccesses, a => a.Product == target);
        Assert.Single(f.Published.OfType<UserRegisteredEvent>());
        if (account == "institution") Assert.Single(f.Admins, a => a.Product == target && a.IsActive);
    }

    // M2a: a parent join needs an active account in another product (speed reading).
    [Theory]
    [InlineData("revoked")]
    [InlineData("course-management-only")]
    [InlineData("no-product")]
    public async Task ParentJoinNeedsAnActiveAccountInTheOtherProduct(string scenario)
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "parent", confirmed: true);
        f.User.RevokeProductAccess(PlatformProduct.SpeedReading, DateTimeOffset.UtcNow);
        if (scenario == "course-management-only")
            f.User.GrantProductAccess(PlatformProduct.CourseManagement, UserProductAccessSource.Admin, Guid.NewGuid(), DateTimeOffset.UtcNow);
        if (scenario == "no-product")
            ((System.Collections.IList)typeof(User).GetField("_productAccesses", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(f.User)!).Clear();

        var result = await ParentHandler(f).Handle(new Identity.Application.Commands.RegisterParent.RegisterParentCommand(
            f.User.Email, EnrollmentFixture.Password, "Yeni", "Veli", null, PlatformProduct.Coaching), CancellationToken.None);

        Assert.Equal("Identity.UserExists", result.Error.Code);
        Assert.Equal("Bu e-posta adresiyle zaten kayıtlı bir hesap var. Lütfen giriş yapın veya şifrenizi sıfırlayın.", result.Error.Description);
        Assert.False(f.User.HasProductAccess(PlatformProduct.Coaching));
        Assert.DoesNotContain(f.User.Roles, r => r.RoleId == f.Roles["Parent"].Id);
        Assert.Empty(f.ParentProfiles);
        Assert.Equal(0, f.Saves);
    }

    // M2b: an MFA account can become a parent from the parent form (code field) and from the join endpoint.
    [Fact]
    public async Task ParentRegistrationAcceptsTheMfaCodeOfTheExistingAccount()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "parent", confirmed: true);
        f.User.EnableMfa("protected", ["recovery"], DateTimeOffset.UtcNow);
        Identity.Application.Commands.RegisterParent.RegisterParentCommand Command(string? code) =>
            new(f.User.Email, EnrollmentFixture.Password, "Yeni", "Veli", null, PlatformProduct.Coaching, MfaCode: code);

        Assert.Equal("Auth.ExistingAccountMfaRequired", (await ParentHandler(f).Handle(Command(null), CancellationToken.None)).Error.Code);
        var result = await ParentHandler(f).Handle(Command("123456"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(RegistrationOutcome.NotRequired, result.Value.VerificationEmail);
        Assert.Contains(f.User.Roles, r => r.Product == PlatformProduct.Coaching && r.RoleId == f.Roles["Parent"].Id);
    }

    [Fact]
    public async Task TheJoinEndpointAcceptsAParentAccount()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "parent", confirmed: true);
        var handler = new Identity.Application.Commands.JoinProduct.JoinProductCommandHandler(f.Enrollment,
            Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")), new AcceptingLegal());

        var result = await handler.Handle(new Identity.Application.Commands.JoinProduct.JoinProductCommand(
            f.User.Email, EnrollmentFixture.Password, "parent", Product: PlatformProduct.Coaching), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Contains(f.User.Roles, r => r.Product == PlatformProduct.Coaching && r.RoleId == f.Roles["Parent"].Id);
    }

    // L2: the idempotent retry happens only after MFA; no token rotation or e-mail without the code.
    [Fact]
    public async Task ARetryOfAnMfaAccountNeedsAFreshCodeBeforeAnythingIsSent()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);
        f.User.EnableMfa("protected", ["recovery"], DateTimeOffset.UtcNow);
        Assert.True((await f.Join(mfaCode: "123456")).IsSuccess);
        var token = f.User.EmailVerificationToken;

        Assert.Equal("Auth.ExistingAccountMfaRequired", (await f.Join()).Error.Code);
        Assert.Equal("Auth.InvalidMfaCode", (await f.Join(mfaCode: "123456")).Error.Code);
        Assert.Equal(token, f.User.EmailVerificationToken);
        Assert.Single(f.Published.OfType<UserRegisteredEvent>());

        var retry = await f.Join(mfaCode: "654321");
        Assert.True(retry.IsSuccess, retry.IsFailure ? retry.Error.Description : "");
        Assert.Equal(RegistrationOutcome.Sent, retry.Value.VerificationEmail);
        Assert.NotEqual(token, f.User.EmailVerificationToken);
        Assert.Equal(2, f.Published.OfType<UserRegisteredEvent>().Count());
        Assert.Equal(101, f.User.LastAcceptedMfaTimeStep);
    }

    // H1: a resend names the product, so a Coaching account gets the Coaching e-mail and link.
    [Theory]
    [InlineData("speed-reading", false, "speed-reading")]
    [InlineData(null, false, "speed-reading")]
    [InlineData(null, true, null)]
    [InlineData("coaching", true, "coaching")]
    [InlineData("not-a-product", false, "speed-reading")]
    public async Task ResendingTheVerificationEmailNamesTheProduct(string? requested, bool bothProducts, string? expected)
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);  // has speed reading only
        if (bothProducts)
        {
            f.User.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            f.AddRole("Student", PlatformProduct.Coaching);
        }
        var handler = new Identity.Application.Commands.ResendVerificationEmail.ResendVerificationEmailCommandHandler(f.Users(), f.UnitOfWork(), f.Publisher());

        var result = await handler.Handle(new(f.User.Email, requested), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, Assert.Single(f.Published.OfType<UserRegisteredEvent>()).Product);
    }

    [Fact]
    public async Task ResendingForAProductTheAccountDoesNotHaveFallsBackToItsOwnProduct()
    {
        var f = new EnrollmentFixture(PlatformProduct.Coaching, "student", confirmed: false);  // speed reading only
        var handler = new Identity.Application.Commands.ResendVerificationEmail.ResendVerificationEmailCommandHandler(f.Users(), f.UnitOfWork(), f.Publisher());
        Assert.True((await handler.Handle(new(f.User.Email, "coaching"), CancellationToken.None)).IsSuccess);
        Assert.Equal("speed-reading", Assert.Single(f.Published.OfType<UserRegisteredEvent>()).Product);
    }

    private static Identity.Application.Commands.RegisterParent.RegisterParentCommandHandler ParentHandler(EnrollmentFixture f) => new(
        f.ExistingIdentity(), f.Users(), Stub<IParentRepository>((m, _) => throw new NotSupportedException(m.Name)), f.UnitOfWork(), f.Publisher(),
        Stub<IConfigurationService>((_, _) => Task.FromResult<string?>("true")), new AcceptingLegal(), f.Enrollment);

    private static IIdentityService NewIdentity(Guid userId) => Stub<IIdentityService>((m, _) => m.Name switch
    {
        nameof(IIdentityService.RegisterUserAsync) => Task.FromResult(Result.Success(userId)),
        nameof(IIdentityService.AssignRoleForProductAsync) => Task.FromResult(Result.Success()),
        _ => throw new NotSupportedException(m.Name)
    });

    private static IUserRepository NewUsers(User user) => Stub<IUserRepository>((m, _) => m.Name switch
    {
        nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(user),
        nameof(IUserRepository.TrackProductAccessIfNew) => null,
        _ => throw new NotSupportedException(m.Name)
    });

    public class OutcomeMediator : DispatchProxy
    {
        public RegistrationOutcome Outcome = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "Send" ? Task.FromResult(Result.Success(Outcome)) : throw new NotSupportedException(targetMethod?.Name);
    }

    /// <summary>The identity API's own MVC JSON options (Program.cs uses <see cref="Identity.API.IdentityApiJson"/>).</summary>
    private static System.Text.Json.JsonSerializerOptions AppJsonOptions()
    {
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        Microsoft.Extensions.DependencyInjection.MvcCoreMvcBuilderExtensions.AddJsonOptions(
            Microsoft.Extensions.DependencyInjection.MvcServiceCollectionExtensions.AddControllers(services),
            options => Identity.API.IdentityApiJson.Configure(options.JsonSerializerOptions));
        using var provider = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(services);
        return Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>(provider).Value.JsonSerializerOptions;
    }

    private sealed class EnrollmentFixture
    {
        public const string Password = "ExistingPassword1!";
        public readonly PlatformProduct Source, Target;
        private readonly string account;
        public readonly User User = User.Create(Guid.NewGuid(), "existing@example.test", "Mevcut", "Hesap");
        public readonly Institution Institution = Institution.Create("Mevcut kurum", InstitutionType.School);
        public readonly Dictionary<string, Role> Roles = new[] { "Student", "Teacher", "InstitutionAdmin", "InstitutionOwner", "Parent" }.ToDictionary(x => x, x => Role.Create(x, x, true));
        public readonly List<object> Published = [];
        public readonly List<InstitutionAdmin> Admins = [];
        public readonly List<ParentProfile> ParentProfiles = [];
        public readonly ExistingAccountEnrollment Enrollment;
        public readonly IInstitutionRepository Institutions;
        public int Saves;

        /// <param name="account">student, teacher, institution or parent (a parent's source account is a student one).</param>
        public EnrollmentFixture(PlatformProduct target, string account, bool confirmed)
        {
            Target = target;
            Source = target == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
            this.account = account;
            var hasher = new PasswordHasher();
            hasher.CreatePasswordHash(Password, out var hash, out var salt);
            User.SetPassword(hash, salt);
            if (confirmed) User.ConfirmEmail();
            User.GrantProductAccess(Source, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            AddRole(account switch { "teacher" => "Teacher", "institution" => "InstitutionAdmin", _ => "Student" }, Source);
            Admins.Add(InstitutionAdmin.Create(User.Id, Institution.Id, InstitutionAdminRole.Manager, Source));
            Institutions = Stub<IInstitutionRepository>((m, args) => m.Name switch
            {
                "GetByIdAsync" => Task.FromResult<Institution?>((Guid)args![0]! == Institution.Id ? Institution : null),
                "GetAdminsForRegistrationAsync" => Task.FromResult<IReadOnlyList<InstitutionAdmin>>(Admins.ToList()),
                "AddAdminAsync" => AddAdmin((InstitutionAdmin)args![0]!),
                "RemoveAdmin" => RemoveAdmin((InstitutionAdmin)args![0]!),
                _ => throw new NotSupportedException(m.Name)
            });
            Enrollment = new ExistingAccountEnrollment(
                Users(),
                Institutions,
                Stub<IRoleRepository>((_, args) => Task.FromResult<Role?>(Roles[(string)args![0]!])), hasher,
                Stub<IMultiFactorService>((_, args) => (string)args![1]! switch { "123456" => (long?)100, "654321" => 101, _ => null }),
                UnitOfWork(), Stub<IRegistrationLegalConsentService>((_, _) => null), TimeProvider.System,
                new TestAuthenticationLock(), Publisher(),
                parents: Stub<IParentRepository>((m, args) => m.Name switch
                {
                    nameof(IParentRepository.GetByUserIdAsync) => Task.FromResult(ParentProfiles.FirstOrDefault(p => p.UserId == (Guid)args![0]!)),
                    nameof(IParentRepository.AddAsync) => AddParent((ParentProfile)args![0]!),
                    _ => throw new NotSupportedException(m.Name)
                }));
        }

        public void AddRole(string name, PlatformProduct product)
        {
            var membership = new Identity.Domain.Entities.UserRole(User.Id, Roles[name].Id, product);
            typeof(Identity.Domain.Entities.UserRole).GetProperty("Role")!.SetValue(membership, Roles[name]);
            User.AddRole(membership);
        }

        public Task<Result<RegistrationOutcome>> Join(string password = Password, string? mfaCode = null) =>
            Enrollment.JoinAsync(User.Email, password, Target, account, [], mfaCode, CancellationToken.None);

        public IUserRepository Users() => Stub<IUserRepository>((m, _) => m.Name switch
        {
            nameof(IUserRepository.GetByEmailAsync) => Task.FromResult<User?>(User),
            nameof(IUserRepository.GetByIdAsync) => Task.FromResult<User?>(User),
            nameof(IUserRepository.TrackProductAccessIfNew) => null,
            _ => throw new NotSupportedException(m.Name)
        });
        public IIdentityService ExistingIdentity() => Stub<IIdentityService>((m, _) => m.Name == nameof(IIdentityService.RegisterUserAsync)
            ? Task.FromResult(Result.Failure<Guid>(new Error("Identity.UserExists", "Bu e-posta adresiyle zaten kayıtlı bir hesap var.")))
            : throw new NotSupportedException(m.Name));
        public IUnitOfWork UnitOfWork() => Stub<IUnitOfWork>((m, _) => m.Name == "SaveChangesAsync" ? Save() : null);
        public IPublishEndpoint Publisher() => Stub<IPublishEndpoint>((_, args) => { Published.Add(args![0]!); return Task.CompletedTask; });
        private Task<int> Save() { Saves++; return Task.FromResult(1); }
        private Task AddAdmin(InstitutionAdmin admin) { Admins.Add(admin); return Task.CompletedTask; }
        private Task AddParent(ParentProfile profile) { ParentProfiles.Add(profile); return Task.CompletedTask; }
        private object? RemoveAdmin(InstitutionAdmin admin) { Admins.Remove(admin); return null; }
        public void SetPassword(string password)
        {
            new PasswordHasher().CreatePasswordHash(password, out var hash, out var salt);
            User.SetPassword(hash, salt);
        }
    }

    private sealed class AcceptingLegal : IRegistrationLegalConsentService
    {
        public Task<Result<IReadOnlyList<PlatformLegalPageDto>>> ValidateAsync(PlatformProduct product,
            IEnumerable<LegalPageAcceptance>? acceptances, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<PlatformLegalPageDto>>([]));
        public void TrackAcceptedDocuments(Guid userId, PlatformProduct product, IReadOnlyList<PlatformLegalPageDto> documents, string registrationMethod) { }
    }

    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Call = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args);
    }
    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, Proxy>();
        ((Proxy)(object)proxy).Call = call;
        return proxy;
    }
}
