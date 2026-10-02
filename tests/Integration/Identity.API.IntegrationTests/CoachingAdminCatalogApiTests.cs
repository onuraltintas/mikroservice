using Coaching.API.Controllers;
using Coaching.Application.CatalogAdministration;
using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminCatalogApiTests
{
    [Theory]
    [InlineData("unknown")]
    [InlineData("99")]
    [InlineData("0")]
    [InlineData("1")]
    public async Task UnknownCatalogTypeReturnsFriendlyBadRequest(string kind)
    {
        var result = await new CoachingCatalogAdminController(new Reader()).List(kind, new(), default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task ValidCatalogReturnsAnEnvelope()
    {
        var result = await new CoachingCatalogAdminController(new Reader()).List("schools", new(), default);
        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task InvalidFilterReturnsFriendlyBadRequest()
    {
        var result = await new CoachingCatalogAdminController(new InvalidReader()).List("schools", new(), default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    private sealed class InvalidReader : ICoachingAdminCatalogReader
    {
        public Task<TargetSearchPage<AdminCatalogRow>> ListAsync(CatalogKind kind, AdminCatalogFilter filter, CancellationToken cancellationToken)
            => throw new ArgumentException("Filtre değerlerini kontrol edin.");
    }

    private sealed class Reader : ICoachingAdminCatalogReader
    {
        public Task<TargetSearchPage<AdminCatalogRow>> ListAsync(CatalogKind kind, AdminCatalogFilter filter, CancellationToken cancellationToken)
            => Task.FromResult(new TargetSearchPage<AdminCatalogRow>([], 0, 1, 25));
    }
}
