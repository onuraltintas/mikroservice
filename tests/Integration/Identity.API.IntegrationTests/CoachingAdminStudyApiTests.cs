using Coaching.API.Controllers;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAdminStudyApiTests
{
    [Fact]
    public void StudentInspectionUsesReadPermissionAndOnlyGetEndpoints()
    {
        var type = typeof(CoachingAdminStudyController);
        Assert.Equal(PlatformPermissions.Coaching.View, type.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
        Assert.Equal(MfaOperationCategories.Coaching, type.GetCustomAttribute<MfaCategoryAttribute>()?.Category);
        foreach (var name in new[] { "Availability", "Plans", "Plan", "Report" })
            Assert.NotNull(type.GetMethod(name)!.GetCustomAttribute<HttpGetAttribute>());
    }
}
