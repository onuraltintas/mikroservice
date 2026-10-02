using System.Reflection;
using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.CatalogAdministration;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogManagementApiTests
{
    [Fact]
    public async Task CreateReturns201AndLocationWhileNumericKindIsRejected()
    {
        var service = new Service();
        var controller = new CoachingCatalogManagementAdminController(service);
        var result = Assert.IsType<CreatedResult>(await controller.Create("lessons", new CatalogSaveRequest("Math", "Yeni kayıt"), default));
        Assert.Contains(service.Id.ToString(), result.Location);
        Assert.IsType<BadRequestObjectResult>(await controller.Create("0", new CatalogSaveRequest("Math", "Yeni kayıt"), default));
        Assert.Equal(1, service.Calls);
    }

    [Theory]
    [InlineData("Catalog.Stale", 409)]
    [InlineData("Catalog.Hierarchy", 409)]
    [InlineData("Catalog.NotFound", 404)]
    public async Task ReturnsFriendlyExpectedStatus(string code, int status)
    {
        var controller = new CoachingCatalogManagementAdminController(new Service { Error = new BusinessRuleException(code, "Güncel kaydı kontrol edin.") });
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.Update("lessons", Guid.NewGuid(), new("Math", "Test kaydı"), default));
        Assert.Equal(status, result.StatusCode);
    }

    [Fact]
    public async Task LocationServiceFailureIs503AndValidationIs400()
    {
        var service = new Service { Error = new HttpRequestException("Internal upstream details") };
        var controller = new CoachingCatalogManagementAdminController(service);
        var unavailable = Assert.IsType<ObjectResult>(await controller.Create("schools", new CatalogSaveRequest("School", "Test kaydı"), default));
        Assert.Equal(503, unavailable.StatusCode);
        Assert.DoesNotContain("Internal", JsonSerializer.Serialize(unavailable.Value));
        service.Error = new ArgumentException("Internal domain details");
        Assert.IsType<BadRequestObjectResult>(await controller.Create("lessons", new CatalogSaveRequest("Math", "Test kaydı"), default));
    }

    [Fact]
    public void AllWritesRequireContentManageAndConfigurableCoachingMfa()
    {
        var type = typeof(CoachingCatalogManagementAdminController);
        Assert.Equal(MfaOperationCategories.Coaching, type.GetCustomAttribute<MfaCategoryAttribute>()?.Category);
        foreach (var method in new[] { "Create", "Update", "SetActive" })
            Assert.Equal(PlatformPermissions.Coaching.ContentManage, type.GetMethod(method)!.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
    }

    private sealed class Service : ICoachingCatalogManagementService
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Exception? Error { get; set; }
        public int Calls { get; private set; }
        private Task<CatalogEditDocument> Result()
        {
            Calls++;
            return Error is null ? Task.FromResult(new CatalogEditDocument("hash", JsonSerializer.SerializeToElement(new { Id, Name = "Math", Source = "admin-manual", SourceId = "1", IsActive = false }))) : Task.FromException<CatalogEditDocument>(Error);
        }
        public Task<CatalogEditDocument> GetAsync(CatalogKind kind, Guid id, CancellationToken ct) => Result();
        public Task<CatalogEditDocument> CreateAsync(CatalogKind kind, CatalogSaveRequest r, CancellationToken ct) => Result();
        public Task<CatalogEditDocument> UpdateAsync(CatalogKind kind, Guid id, CatalogSaveRequest r, CancellationToken ct) => Result();
        public Task<CatalogEditDocument> SetActiveAsync(CatalogKind kind, Guid id, CatalogStatusRequest r, CancellationToken ct) => Result();
    }
}
