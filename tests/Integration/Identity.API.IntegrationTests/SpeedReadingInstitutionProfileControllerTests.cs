using System.Reflection;
using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.API.Controllers;
using Identity.Application.DTOs.Institutions;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingInstitutionProfileControllerTests
{
    [Fact]
    public async Task GetMe_ResolvesOnlyTheAuthenticatedUsersSpeedReadingInstitution()
    {
        var userId = Guid.NewGuid();
        var institutionId = Guid.NewGuid();
        var repository = DispatchProxy.Create<IInstitutionRepository, InstitutionRepositoryProxy>();
        var capture = (InstitutionRepositoryProxy)(object)repository;
        capture.ManagedInstitutionId = institutionId;
        capture.Institution = CreateInstitution(institutionId);
        var controller = new SpeedReadingInstitutionProfileController(
            new TestCurrentUserService(userId),
            repository);

        var result = await controller.GetMe(CancellationToken.None);

        var response = result.Result.Should().BeOfType<OkObjectResult>().Subject.Value;
        response.Should().BeEquivalentTo(new CurrentSpeedReadingInstitutionResponse(institutionId, "Örnek Kurum"));
        capture.RequestedAdminUserId.Should().Be(userId);
        capture.RequestedProduct.Should().Be(PlatformProduct.SpeedReading);
        capture.RequestedInstitutionId.Should().Be(institutionId);
    }

    [Fact]
    public async Task GetMe_ForbidsAccountsWithoutASpeedReadingInstitutionAssignment()
    {
        var repository = DispatchProxy.Create<IInstitutionRepository, InstitutionRepositoryProxy>();
        var capture = (InstitutionRepositoryProxy)(object)repository;
        var controller = new SpeedReadingInstitutionProfileController(
            new TestCurrentUserService(Guid.NewGuid()),
            repository);

        var result = await controller.GetMe(CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        capture.RequestedProduct.Should().Be(PlatformProduct.SpeedReading);
        capture.RequestedInstitutionId.Should().BeNull();
    }

    [Fact]
    public async Task GetMe_ReturnsUnauthorizedWhenThereIsNoAuthenticatedUserId()
    {
        var repository = DispatchProxy.Create<IInstitutionRepository, InstitutionRepositoryProxy>();
        var capture = (InstitutionRepositoryProxy)(object)repository;
        var controller = new SpeedReadingInstitutionProfileController(
            new TestCurrentUserService(null),
            repository);

        var result = await controller.GetMe(CancellationToken.None);

        result.Result.Should().BeOfType<UnauthorizedResult>();
        capture.RequestedProduct.Should().BeNull();
    }

    [Fact]
    public void ControllerRequiresInstitutionRoleSpeedReadingPermissionAndMfaCategory()
    {
        var controller = typeof(SpeedReadingInstitutionProfileController);
        controller.GetCustomAttributes<AuthorizeAttribute>(inherit: true)
            .Should().Contain(attribute => attribute.Roles == "InstitutionAdmin,InstitutionOwner");
        controller.GetCustomAttributes<HasPermissionAttribute>(inherit: true)
            .Should().Contain(attribute => attribute.Policy == PlatformPermissions.Institutions.Manage);
        controller.GetCustomAttributes<MfaCategoryAttribute>(inherit: true)
            .Should().ContainSingle(attribute => attribute.Category == MfaOperationCategories.SpeedReading);
        controller.GetMethod(nameof(SpeedReadingInstitutionProfileController.GetMe))
            .Should().NotBeNull();
    }

    private static InstitutionDto CreateInstitution(Guid id) => new(
        id, "Örnek Kurum", default, null, null, null, null, null, null, null, null, null,
        default, 100, 10, null, null, true, 0, 0, 1, DateTime.UtcNow);

    private sealed class TestCurrentUserService(Guid? userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => "manager@example.test";
        public string? FullName => "Institution Manager";
        public IEnumerable<string> Roles => ["InstitutionAdmin"];
        public bool IsAuthenticated => userId.HasValue;
        public ClaimsPrincipal? User => new(new ClaimsIdentity([], "test"));
    }

    public sealed class InstitutionRepositoryProxy : DispatchProxy
    {
        public Guid? ManagedInstitutionId { get; set; }
        public InstitutionDto? Institution { get; set; }
        public Guid? RequestedAdminUserId { get; private set; }
        public PlatformProduct? RequestedProduct { get; private set; }
        public Guid? RequestedInstitutionId { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IInstitutionRepository.GetInstitutionIdByAdminIdAsync))
            {
                RequestedAdminUserId = (Guid)args![0]!;
                RequestedProduct = (PlatformProduct)args[1]!;
                return Task.FromResult(ManagedInstitutionId);
            }

            if (targetMethod?.Name == nameof(IInstitutionRepository.GetDtoByIdAsync))
            {
                RequestedInstitutionId = (Guid)args![0]!;
                return Task.FromResult(Institution?.Id == RequestedInstitutionId ? Institution : null);
            }

            throw new NotSupportedException($"Unexpected institution repository call: {targetMethod?.Name}");
        }
    }
}
