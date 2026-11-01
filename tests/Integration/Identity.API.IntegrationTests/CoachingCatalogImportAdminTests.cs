using System.Reflection;
using Coaching.API.Controllers;
using Coaching.Infrastructure.Catalogs;
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

    [Fact]
    public void ImportAcceptsExactlyTheFivePreflightFiles()
    {
        // The admin page sends these five files (coaching-catalog-import.ts); the controller used to demand six.
        var five = new[] { "lessons.json", "units-derived.json", "upper-subjects.json", "subjects.json", "lgs-programs.json" }
            .ToDictionary(x => x, _ => "[]");
        Assert.Equal(five.Keys.OrderBy(x => x), CatalogPreflight.RequiredFileNames.OrderBy(x => x));
        Assert.True(CatalogPreflight.HasRequiredFiles(five));
        Assert.False(CatalogPreflight.HasRequiredFiles(new Dictionary<string, string>(five) { ["extra.json"] = "[]" }));
        Assert.False(CatalogPreflight.HasRequiredFiles(five.Where(x => x.Key != "subjects.json").ToDictionary()));
        Assert.False(CatalogPreflight.HasRequiredFiles(five.Where(x => x.Key != "subjects.json").Append(new("other.json", "[]")).ToDictionary()));
        Assert.False(CatalogPreflight.HasRequiredFiles(null));
    }
}
