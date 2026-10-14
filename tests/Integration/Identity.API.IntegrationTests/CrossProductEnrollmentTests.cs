using System.Reflection;
using EduPlatform.Shared.Security.Services;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class CrossProductEnrollmentTests
{
    [Theory]
    [InlineData(PlatformProduct.Coaching, "student")]
    [InlineData(PlatformProduct.SpeedReading, "student")]
    [InlineData(PlatformProduct.Coaching, "teacher")]
    [InlineData(PlatformProduct.SpeedReading, "teacher")]
    [InlineData(PlatformProduct.Coaching, "institution")]
    [InlineData(PlatformProduct.SpeedReading, "institution")]
    public async Task BothDirectionsPreserveIdentityAndInstitution(PlatformProduct target, string account)
    {
        var fixture = new Fixture(target, account);
        var passwordHash = fixture.User.PasswordHash.ToArray();
        var studentId = fixture.Student.Id;
        var teacherId = fixture.Teacher.Id;
        var result = await fixture.Join();
        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : "");
        Assert.Equal(fixture.User.Id, result.Value);
        Assert.Equal(passwordHash, fixture.User.PasswordHash);
        Assert.Equal("Original", fixture.User.FirstName);
        Assert.True(fixture.User.HasProductAccess(fixture.Source));
        Assert.True(fixture.User.HasProductAccess(target));
        Assert.Equal(studentId, fixture.Student.Id);
        Assert.Equal(teacherId, fixture.Teacher.Id);
        Assert.Equal(fixture.Institution.Id, fixture.Student.InstitutionId);
        Assert.Equal(fixture.Institution.Id, fixture.Teacher.InstitutionId);
        Assert.Equal(0, fixture.ProfilesAdded);
        Assert.Equal(1, fixture.Saves);
        Assert.Equal(1, fixture.Consents);
        if (account == "institution")
        {
            var link = Assert.Single(fixture.AddedAdmins);
            Assert.Equal(fixture.Institution.Id, link.InstitutionId);
            Assert.Equal(target, link.Product);
            Assert.Equal(InstitutionAdminRole.Manager, link.Role);
            Assert.DoesNotContain(fixture.User.Roles, r => r.Product == target && r.RoleId == fixture.Roles["InstitutionOwner"].Id);
        }
    }

    [Theory]
    [InlineData("password", "Auth.ExistingAccountPasswordRequired")]
    [InlineData("inactive", "Auth.ExistingAccountNotEligible")]
    [InlineData("unconfirmed", "Auth.ExistingAccountNotEligible")]
    [InlineData("wrong-role", "Auth.SourceRoleRequired")]
    [InlineData("revoked", "Identity.UserExists")]
    [InlineData("inactive-profile", "Auth.ExistingAccountNotEligible")]
    [InlineData("inactive-institution", "Auth.ExistingAccountNotEligible")]
    [InlineData("mfa-missing", "Auth.ExistingAccountMfaRequired")]
    [InlineData("mfa-invalid", "Auth.InvalidMfaCode")]
    public async Task RejectedEnrollmentDoesNotGrantAccess(string scenario, string code)
    {
        var f = new Fixture(PlatformProduct.Coaching, "teacher");
        if (scenario == "inactive") f.User.Deactivate();
        if (scenario == "unconfirmed") typeof(User).GetProperty(nameof(User.EmailConfirmed))!.SetValue(f.User, false);
        if (scenario == "revoked") { f.User.GrantProductAccess(f.Target, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow); f.User.RevokeProductAccess(f.Target, DateTimeOffset.UtcNow); }
        if (scenario == "inactive-profile") f.Teacher.Deactivate();
        if (scenario == "inactive-institution") f.Institution.Deactivate();
        if (scenario.StartsWith("mfa")) f.User.EnableMfa("protected", ["recovery"], DateTimeOffset.UtcNow);
        var result = await f.Join(scenario == "password" ? "WrongPassword1!" : Fixture.Password,
            scenario == "wrong-role" ? "institution" : null, scenario == "mfa-invalid" ? "000000" : null);
        Assert.Equal(code, result.Error.Code);
        Assert.False(f.User.HasProductAccess(f.Target));
        Assert.Equal(0, f.ProfilesAdded);
        Assert.Empty(f.AddedAdmins);
        Assert.Equal(0, f.Consents);
    }

    [Fact]
    public async Task MfaEnrollmentRequiresFreshCodeAndDoesNotDisableMfa()
    {
        var f = new Fixture(PlatformProduct.SpeedReading, "student");
        f.User.EnableMfa("protected", ["recovery"], DateTimeOffset.UtcNow);
        Assert.True((await f.Join(mfaCode: "123456")).IsSuccess);
        Assert.True(f.User.MfaEnabled);
        Assert.Equal("protected", f.User.MfaSecretProtected);
        Assert.Equal(100, f.User.LastAcceptedMfaTimeStep);
    }

    [Fact]
    public async Task SaveFailureIsPropagatedWithoutDeletingIdentity()
    {
        var f = new Fixture(PlatformProduct.Coaching, "institution") { FailSave = true };
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Join());
        Assert.True(f.User.HasProductAccess(f.Source));
    }

    private sealed class Fixture
    {
        public const string Password = "ExistingPassword1!";
        public readonly PlatformProduct Source, Target;
        private readonly string account;
        public User User = User.Create(Guid.NewGuid(), "existing@example.test", "Original", "Account");
        public Institution Institution = Institution.Create("Existing institution", InstitutionType.School);
        public StudentProfile Student;
        public TeacherProfile Teacher;
        public Dictionary<string, Role> Roles = new[] { "Student", "Teacher", "InstitutionAdmin", "InstitutionOwner" }.ToDictionary(x => x, x => Role.Create(x, x, true));
        public List<InstitutionAdmin> AddedAdmins = [];
        public int Saves, ProfilesAdded, Consents;
        public bool FailSave;
        private readonly ExistingAccountEnrollment enrollment;

        public Fixture(PlatformProduct target, string account)
        {
            Target = target; Source = target == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
            this.account = account;
            var hasher = new PasswordHasher();
            hasher.CreatePasswordHash(Password, out var hash, out var salt); User.SetPassword(hash, salt); User.ConfirmEmail();
            User.GrantProductAccess(Source, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
            var role = Roles[account == "student" ? "Student" : account == "teacher" ? "Teacher" : "InstitutionAdmin"];
            var membership = new Identity.Domain.Entities.UserRole(User.Id, role.Id, Source);
            typeof(Identity.Domain.Entities.UserRole).GetProperty("Role")!.SetValue(membership, role); User.AddRole(membership);
            Student = StudentProfile.Create(User.Id, "Original", "Account", Institution.Id);
            Teacher = TeacherProfile.Create(User.Id, "Original", "Account", Institution.Id);
            enrollment = new ExistingAccountEnrollment(
                Stub<IUserRepository>((m, _) => m.Name == "GetByEmailAsync" ? Task.FromResult<User?>(User) : null),
                Stub<IStudentRepository>((m, _) => m.Name == "GetExistingForRegistrationAsync" ? Task.FromResult<StudentProfile?>(Student) : AddProfile()),
                Stub<ITeacherRepository>((m, _) => m.Name == "GetExistingForRegistrationAsync" ? Task.FromResult<TeacherProfile?>(Teacher) : AddProfile()),
                Stub<IInstitutionRepository>((m, args) => m.Name switch {
                    "GetByIdAsync" => Task.FromResult<Institution?>(Institution),
                    "GetAdminsForRegistrationAsync" => Task.FromResult<IReadOnlyList<InstitutionAdmin>>([InstitutionAdmin.Create(User.Id, Institution.Id, InstitutionAdminRole.Manager, Source)]),
                    "AddAdminAsync" => AddAdmin((InstitutionAdmin)args![0]!),
                    _ => throw new NotSupportedException(m.Name) }),
                Stub<IRoleRepository>((_, args) => Task.FromResult<Role?>(Roles[(string)args![0]!])), hasher,
                Stub<IMultiFactorService>((_, args) => (string)args![1]! == "123456" ? (long?)100 : null),
                Stub<IUnitOfWork>((m, _) => m.Name == "SaveChangesAsync" ? Save() : null),
                Stub<IRegistrationLegalConsentService>((_, _) => { Consents++; return null; }), TimeProvider.System, new TestAuthenticationLock());
        }
        public Task<EduPlatform.Shared.Kernel.Results.Result<Guid>> Join(string password = Password, string? role = null, string? mfaCode = null) => enrollment.JoinAsync(User.Email, password, Target, role ?? account, [], mfaCode, CancellationToken.None);
        private Task AddProfile() { ProfilesAdded++; return Task.CompletedTask; }
        private Task AddAdmin(InstitutionAdmin admin) { AddedAdmins.Add(admin); return Task.CompletedTask; }
        private Task<int> Save() { Saves++; if (FailSave) throw new InvalidOperationException("Write failed"); return Task.FromResult(1); }
    }
    public class Proxy : DispatchProxy
    {
        public Func<MethodInfo, object?[]?, object?> Call = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args);
    }
    private static T Stub<T>(Func<MethodInfo, object?[]?, object?> call) where T : class { var proxy = DispatchProxy.Create<T, Proxy>(); ((Proxy)(object)proxy).Call = call; return proxy; }
}
