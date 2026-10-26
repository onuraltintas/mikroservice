using Coaching.API.Controllers;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTargetSearchApiTests
{
    [Fact]
    public void AdmissionApiRequiresSignedInCoachingUsersAndBoundsSearchTraffic()
    {
        var type = typeof(TargetAdmissionsController);
        Assert.Equal("Student,Teacher,InstitutionAdmin,InstitutionOwner", Assert.Single(type.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>()).Roles);
        Assert.Equal("api/coaching/study-planning/targets/admissions", Assert.Single(type.GetCustomAttributes(typeof(RouteAttribute), false).Cast<RouteAttribute>()).Template);
        Assert.Equal("target-catalog-search", Assert.Single(type.GetCustomAttributes(typeof(EnableRateLimitingAttribute), false).Cast<EnableRateLimitingAttribute>()).PolicyName);
    }

    [Fact]
    public void MebiFiveFieldFiltersAcceptYdtAndNormalizeLegacyLanguageAliases()
    {
        TargetAdmissionSearchService.Validate(new("YKS", 2026, ProgramType: "Bachelor", University: "Ankara",
            Faculty: "Engineering", ProgramName: "Computer", ScoreType: "YDT"));
        TargetAdmissionSearchService.Validate(new("YKS", 2026, ScoreType: "DİL"));
        Assert.Throws<ArgumentException>(() => TargetAdmissionSearchService.Validate(new("YKS", 2026, ProgramType: "Doctorate")));
        Assert.Throws<ArgumentException>(() => TargetAdmissionSearchService.Validate(new("YKS", 2026, Search: "legacy search")));
    }
}
