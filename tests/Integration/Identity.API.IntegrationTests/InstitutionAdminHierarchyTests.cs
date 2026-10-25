using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.Authorization;
using Identity.Application.DTOs.Institutions;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class InstitutionAdminHierarchyTests
{
    private static readonly Guid InstitutionId = Guid.NewGuid();
    private const PlatformProduct Product = PlatformProduct.Coaching;

    [Theory]
    // caller,                        target now,                  granting,                    allowed
    [InlineData(InstitutionAdminRole.Owner, InstitutionAdminRole.Admin, null, true)]
    [InlineData(InstitutionAdminRole.Owner, null, InstitutionAdminRole.Owner, true)]
    [InlineData(InstitutionAdminRole.Admin, InstitutionAdminRole.Manager, null, true)]
    [InlineData(InstitutionAdminRole.Admin, null, InstitutionAdminRole.Manager, true)]
    [InlineData(InstitutionAdminRole.Admin, InstitutionAdminRole.Admin, null, false)]
    [InlineData(InstitutionAdminRole.Admin, InstitutionAdminRole.Owner, null, false)]
    [InlineData(InstitutionAdminRole.Admin, null, InstitutionAdminRole.Owner, false)]
    [InlineData(InstitutionAdminRole.Manager, InstitutionAdminRole.Owner, null, false)]
    [InlineData(InstitutionAdminRole.Manager, null, InstitutionAdminRole.Owner, false)]
    [InlineData(InstitutionAdminRole.Manager, InstitutionAdminRole.Manager, null, false)]
    public async Task OnlyHigherRanksManageAdminMemberships(
        InstitutionAdminRole caller,
        InstitutionAdminRole? targetNow,
        InstitutionAdminRole? granting,
        bool allowed)
    {
        var callerId = Guid.NewGuid();
        var authorization = Create(callerId, [Admin(callerId, caller), Admin(Guid.NewGuid(), InstitutionAdminRole.Owner)]);

        var result = await authorization.EnsureCanManageInstitutionAdminAsync(
            InstitutionId, Product, Guid.NewGuid(), targetNow, granting, deactivating: false, CancellationToken.None);

        result.IsSuccess.Should().Be(allowed);
    }

    [Fact]
    public async Task AnOwnerMayDeactivateACoOwner_AnInactiveMembershipManagesNothing()
    {
        var callerId = Guid.NewGuid();
        var coOwnerId = Guid.NewGuid();
        var active = Create(callerId, [Admin(callerId, InstitutionAdminRole.Owner), Admin(coOwnerId, InstitutionAdminRole.Owner)]);
        var inactive = Create(callerId, [Admin(callerId, InstitutionAdminRole.Owner, active: false), Admin(coOwnerId, InstitutionAdminRole.Owner)]);

        (await active.EnsureCanManageInstitutionAdminAsync(
            InstitutionId, Product, coOwnerId, InstitutionAdminRole.Owner, null, deactivating: true, CancellationToken.None))
            .IsSuccess.Should().BeTrue("the caller remains an active owner");
        (await inactive.EnsureCanManageInstitutionAdminAsync(
            InstitutionId, Product, coOwnerId, InstitutionAdminRole.Owner, null, deactivating: true, CancellationToken.None))
            .IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task NobodyChangesTheirOwnMembership()
    {
        var callerId = Guid.NewGuid();
        var authorization = Create(callerId, [Admin(callerId, InstitutionAdminRole.Owner), Admin(Guid.NewGuid(), InstitutionAdminRole.Owner)]);

        var result = await authorization.EnsureCanManageInstitutionAdminAsync(
            InstitutionId, Product, callerId, InstitutionAdminRole.Owner, null, deactivating: true, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SystemAdministrator_IsNotLimitedByTheInstitutionHierarchy()
    {
        var authorization = Create(Guid.NewGuid(), [], roles: ["SystemAdmin"]);

        var result = await authorization.EnsureCanManageInstitutionAdminAsync(
            InstitutionId, Product, Guid.NewGuid(), InstitutionAdminRole.Owner, null, deactivating: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(MfaPolicyModes.Required, MfaPolicyModes.MutationsOnly, true)]
    [InlineData(MfaPolicyModes.Required, MfaPolicyModes.Disabled, true)]
    [InlineData(MfaPolicyModes.MutationsOnly, MfaPolicyModes.Disabled, true)]
    [InlineData(null, MfaPolicyModes.Disabled, true)]
    [InlineData(MfaPolicyModes.Disabled, MfaPolicyModes.Required, false)]
    [InlineData(MfaPolicyModes.Disabled, MfaPolicyModes.MutationsOnly, false)]
    [InlineData(MfaPolicyModes.Required, MfaPolicyModes.Required, false)]
    public void MfaPolicy_WeakeningIsDetected(string? current, string requested, bool weakens)
    {
        MfaPolicyChangeRules.Weakens(current, requested).Should().Be(weakens);
    }

    [Fact]
    public void MfaPolicy_StepUpIsRecognisedOnlyFromTheMfaAuthenticationMethod()
    {
        var password = new ClaimsPrincipal(new ClaimsIdentity([new Claim("amr", "pwd")], "Bearer"));
        var mfa = new ClaimsPrincipal(new ClaimsIdentity([new Claim("amr", "pwd"), new Claim("amr", "mfa")], "Bearer"));

        MfaPolicyChangeRules.IsMfaVerified(password).Should().BeFalse();
        MfaPolicyChangeRules.IsMfaVerified(mfa).Should().BeTrue();
        MfaPolicyChangeRules.IsMfaVerified(new ClaimsPrincipal(new ClaimsIdentity([new Claim("amr", "mfa")]))).Should().BeFalse("unauthenticated");
    }

    private static InstitutionAdmin Admin(Guid userId, InstitutionAdminRole role, bool active = true)
    {
        var admin = InstitutionAdmin.Create(userId, InstitutionId, role, Product);
        if (!active) admin.Deactivate();
        return admin;
    }

    private static InstitutionManagementAuthorization Create(
        Guid callerId,
        IReadOnlyList<InstitutionAdmin> admins,
        string[]? roles = null)
    {
        var currentUser = DispatchProxy.Create<ICurrentUserService, CurrentUserProxy>();
        ((CurrentUserProxy)(object)currentUser).Set(callerId, roles ?? ["InstitutionOwner"]);
        var repository = DispatchProxy.Create<IInstitutionRepository, InstitutionRepositoryProxy>();
        ((InstitutionRepositoryProxy)(object)repository).Admins = admins;
        return new InstitutionManagementAuthorization(currentUser, repository);
    }

    public class CurrentUserProxy : DispatchProxy
    {
        private Guid _userId;
        private string[] _roles = [];

        public void Set(Guid userId, string[] roles)
        {
            _userId = userId;
            _roles = roles;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "get_UserId" => (Guid?)_userId,
            "get_Roles" => _roles,
            "get_IsAuthenticated" => true,
            "get_User" => new ClaimsPrincipal(new ClaimsIdentity([new Claim("platform_product", "coaching")], "Bearer")),
            _ => null
        };
    }

    public class InstitutionRepositoryProxy : DispatchProxy
    {
        public IReadOnlyList<InstitutionAdmin> Admins { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            switch (targetMethod?.Name)
            {
                case nameof(IInstitutionRepository.GetAdminAsync):
                    var userId = (Guid)args![1]!;
                    return Task.FromResult(Admins.FirstOrDefault(admin => admin.UserId == userId));
                case nameof(IInstitutionRepository.GetAdminsAsync):
                    IReadOnlyList<InstitutionAdminDto> dtos = Admins
                        .Select(admin => new InstitutionAdminDto(admin.UserId, "x@example.test", "X", "Y", admin.Role, admin.IsActive))
                        .ToList();
                    return Task.FromResult(dtos);
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
