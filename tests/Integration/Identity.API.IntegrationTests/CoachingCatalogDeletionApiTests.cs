using Coaching.API.Controllers;
using Coaching.Application.CatalogAdministration;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogDeletionApiTests
{
    [Theory]
    [InlineData("Catalog.InUse", 409)]
    [InlineData("Catalog.Stale", 409)]
    [InlineData("Catalog.NotFound", 404)]
    public async Task DeleteReturnsFriendlyConflictOrNotFound(string code, int status)
    {
        var result = await new CoachingCatalogDeletionAdminController(new Service(code)).Delete("schools", Guid.NewGuid(), new("hash", "reason", Guid.NewGuid()), default);
        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
    }

    [Fact]
    public async Task NumericCatalogNameIsRejectedBeforeDeletion()
    {
        var result = await new CoachingCatalogDeletionAdminController(new Service(null)).Delete("0", Guid.NewGuid(), new("hash", "reason", Guid.NewGuid()), default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class Service(string? code) : ICoachingCatalogDeletionService
    {
        public Task<CatalogUsage> GetUsageAsync(CatalogKind kind, Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteAsync(CatalogKind kind, Guid id, CatalogDeleteRequest request, CancellationToken cancellationToken)
            => code is null ? Task.CompletedTask : throw new BusinessRuleException(code, "Kayıt silinemez.");
    }
}
