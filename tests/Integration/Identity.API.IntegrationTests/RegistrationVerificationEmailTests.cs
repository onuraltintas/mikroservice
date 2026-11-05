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
            var json = System.Text.Json.JsonSerializer.Serialize(body, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            Assert.Equal($"{{\"userId\":\"{userId}\",\"verificationEmail\":\"{outcome}\"}}", json);
        }
    }

    private sealed class OutcomeMediator : DispatchProxy
    {
        public RegistrationOutcome Outcome = null!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == "Send" ? Task.FromResult(Result.Success(Outcome)) : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class EnrollmentFixture
    {
        public const string Password = "ExistingPassword1!";
        public readonly PlatformProduct Source, Target;
        private readonly string account;
        public readonly User User = User.Create(Guid.NewGuid(), "existing@example.test", "Mevcut", "Hesap");
        public readonly Institution Institution = Institution.Create("Mevcut kurum", InstitutionType.School);
        public readonly Dictionary<string, Role> Roles = new[] { "Student", "Teacher", "InstitutionAdmin", "InstitutionOwner" }.ToDictionary(x => x, x => Role.Create(x, x, true));
        public readonly List<object> Published = [];
        public readonly ExistingAccountEnrollment Enrollment;
        public int Saves;

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
            var role = Roles[account == "student" ? "Student" : account == "teacher" ? "Teacher" : "InstitutionAdmin"];
            var membership = new Identity.Domain.Entities.UserRole(User.Id, role.Id, Source);
            typeof(Identity.Domain.Entities.UserRole).GetProperty("Role")!.SetValue(membership, role);
            User.AddRole(membership);
            var sourceAdmins = new List<InstitutionAdmin> { InstitutionAdmin.Create(User.Id, Institution.Id, InstitutionAdminRole.Manager, Source) };
            Enrollment = new ExistingAccountEnrollment(
                Users(),
                Stub<IInstitutionRepository>((m, args) => m.Name switch
                {
                    "GetByIdAsync" => Task.FromResult<Institution?>((Guid)args![0]! == Institution.Id ? Institution : null),
                    "GetAdminsForRegistrationAsync" => Task.FromResult<IReadOnlyList<InstitutionAdmin>>(sourceAdmins),
                    "AddAdminAsync" => Task.CompletedTask,
                    _ => throw new NotSupportedException(m.Name)
                }),
                Stub<IRoleRepository>((_, args) => Task.FromResult<Role?>(Roles[(string)args![0]!])), hasher,
                Stub<IMultiFactorService>((m, _) => throw new NotSupportedException(m.Name)),
                UnitOfWork(), Stub<IRegistrationLegalConsentService>((_, _) => null), TimeProvider.System,
                new TestAuthenticationLock(), Publisher());
        }

        public Task<Result<RegistrationOutcome>> Join(string password = Password) =>
            Enrollment.JoinAsync(User.Email, password, Target, account, [], null, CancellationToken.None);

        public IUserRepository Users() => Stub<IUserRepository>((m, _) => m.Name switch
        {
            nameof(IUserRepository.GetByEmailAsync) => Task.FromResult<User?>(User),
            nameof(IUserRepository.TrackProductAccessIfNew) => null,
            _ => throw new NotSupportedException(m.Name)
        });
        public IIdentityService ExistingIdentity() => Stub<IIdentityService>((m, _) => m.Name == nameof(IIdentityService.RegisterUserAsync)
            ? Task.FromResult(Result.Failure<Guid>(new Error("Identity.UserExists", "Bu e-posta adresiyle zaten kayıtlı bir hesap var.")))
            : throw new NotSupportedException(m.Name));
        public IUnitOfWork UnitOfWork() => Stub<IUnitOfWork>((m, _) => m.Name == "SaveChangesAsync" ? Save() : null);
        public IPublishEndpoint Publisher() => Stub<IPublishEndpoint>((_, args) => { Published.Add(args![0]!); return Task.CompletedTask; });
        private Task<int> Save() { Saves++; return Task.FromResult(1); }
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
