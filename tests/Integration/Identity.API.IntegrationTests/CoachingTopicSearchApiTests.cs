using Coaching.API.Controllers;
using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTopicSearchApiTests
{
    [Fact]
    public void Api_RequiresStudentAndLimitsTraffic()
    {
        var type = typeof(StudyTopicCatalogController);
        Assert.Equal("Student", Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("target-catalog-search", Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>()).PolicyName);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Api_ForwardsFiltersAndMapsInvalidRequests(bool invalid)
    {
        var stub = new Stub { Invalid = invalid };
        var result = Assert.IsAssignableFrom<ObjectResult>(await new StudyTopicCatalogController(stub).Search("math", 8, "LGS", 2, 20));
        Assert.Equal(invalid ? 400 : 200, result.StatusCode);
        Assert.Equal(("math", 8, "LGS", 2, 20), stub.Request);
    }
    private sealed class Stub : IStudyTopicSearchService
    {
        public bool Invalid { get; init; }
        public (string?, int?, string?, int, int) Request;
        public Task<TargetSearchPage<StudyTopicView>> SearchAsync(string? search, int? gradeNumber, string? examCode,
            int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            Request = (search, gradeNumber, examCode, pageNumber, pageSize);
            return Invalid ? Task.FromException<TargetSearchPage<StudyTopicView>>(new ArgumentException())
                : Task.FromResult(new TargetSearchPage<StudyTopicView>([], 0, pageNumber, pageSize));
        }
    }
}
