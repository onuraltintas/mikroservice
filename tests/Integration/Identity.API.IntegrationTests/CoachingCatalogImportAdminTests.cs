using System.Reflection;
using Coaching.API.Controllers;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogImportAdminTests
{
    [Fact]
    public void ImportAndPublicationHaveSeparateEndpointsAndManagementPermissions()
    {
        var type = typeof(CoachingCatalogImportAdminController);
        Assert.Equal(MfaOperationCategories.Coaching, type.GetCustomAttribute<MfaCategoryAttribute>()?.Category);
        foreach (var method in new[] { "Preview", "Import", "Publish" })
            Assert.Equal(PlatformPermissions.Coaching.ContentManage, type.GetMethod(method)!.GetCustomAttribute<HasPermissionAttribute>()?.Permission);
        Assert.NotEqual(type.GetMethod("Import")!.GetCustomAttribute<HttpPostAttribute>()?.Template,
            type.GetMethod("Publish")!.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }
}
