using Coaching.API.Controllers;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using System.Reflection;
namespace Identity.API.IntegrationTests;
public sealed class CoachingAdminStudyCorrectionApiTests
{
    [Fact]
    public void CorrectionPermissionIsSeparateFromCatalogAndUsesConfiguredMfa()
    {
        var type = typeof(CoachingAdminStudyCorrectionsController);
        Assert.Equal(PlatformPermissions.Coaching.Manage,type.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
        Assert.Equal(MfaOperationCategories.Coaching,type.GetCustomAttribute<MfaCategoryAttribute>()?.Category);
    }
}
