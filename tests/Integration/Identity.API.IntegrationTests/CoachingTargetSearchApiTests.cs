using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTargetSearchApiTests
{
    [Fact]
    public void Api_RequiresStudentAndBoundsSearchTraffic()
    {
        var type = typeof(TargetCatalogController);
        Assert.Equal("Student", Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("api/coaching/study-planning/targets", Assert.Single(type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>()).Template);
        Assert.Equal("target-catalog-search", Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>()).PolicyName);
    }

    [Fact]
    public async Task Api_ForwardsSchoolFiltersPagingAndCancellationAndReturnsEnvelope()
    {
        var stub = new Stub();
        var controller = new TargetCatalogController(stub);
        using var cancellation = new CancellationTokenSource();
        var result = Assert.IsType<OkObjectResult>(await controller.SearchSchools("school", "city", "district", 2, 5, cancellation.Token));
        Assert.Equal(("school", "city", "district", 2, 5, cancellation.Token), stub.SchoolRequest);
        var json = JsonSerializer.SerializeToElement(result.Value);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal(2, json.GetProperty("data").GetProperty("PageNumber").GetInt32());
    }

    [Fact]
    public async Task Api_ForwardsProgramFiltersAndReturnsEmptySuccess()
    {
        var stub = new Stub();
        var result = Assert.IsType<OkObjectResult>(await new TargetCatalogController(stub).SearchPrograms("program", "SAY", 1, 20));
        Assert.Equal(("program", "SAY", 1, 20), stub.ProgramRequest);
        Assert.Empty(JsonSerializer.SerializeToElement(result.Value).GetProperty("data").GetProperty("Items").EnumerateArray());
    }

    [Fact]
    public async Task Api_InvalidFiltersReturn400WithoutInternalDetails()
    {
        var controller = new TargetCatalogController(new Stub { Fail = true });
        foreach (var result in new[] { await controller.SearchSchools(), await controller.SearchPrograms() })
        {
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            var json = JsonSerializer.SerializeToElement(bad.Value);
            Assert.Equal("StudyPlanning.Validation", json.GetProperty("code").GetString());
            Assert.DoesNotContain("internal", json.GetProperty("message").GetString());
        }
    }

    private sealed class Stub : ITargetSearchService
    {
        public bool Fail { get; init; }
        public (string?, string?, string?, int, int, CancellationToken) SchoolRequest;
        public (string?, string?, int, int) ProgramRequest;
        public Task<TargetSearchPage<SchoolTargetView>> SearchSchoolsAsync(string? search, string? city, string? district,
            int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            SchoolRequest = (search, city, district, pageNumber, pageSize, cancellationToken);
            return Fail ? Task.FromException<TargetSearchPage<SchoolTargetView>>(new ArgumentException("internal"))
                : Task.FromResult(new TargetSearchPage<SchoolTargetView>([], 0, pageNumber, pageSize));
        }
        public Task<TargetSearchPage<UniversityTargetView>> SearchProgramsAsync(string? search, string? scoreType,
            int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            ProgramRequest = (search, scoreType, pageNumber, pageSize);
            return Fail ? Task.FromException<TargetSearchPage<UniversityTargetView>>(new ArgumentException("internal"))
                : Task.FromResult(new TargetSearchPage<UniversityTargetView>([], 0, pageNumber, pageSize));
        }
    }
}
