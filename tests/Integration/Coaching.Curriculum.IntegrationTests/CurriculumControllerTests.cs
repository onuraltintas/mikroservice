using System.Reflection;
using Coaching.API.Controllers;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class CurriculumControllerTests
{
    [Fact]
    public void AllWritesRequireContentPermission_AndEveryEndpointRequiresAuthenticationAndMfa()
    {
        var type=typeof(CoachingCurriculumManagementAdminController);
        Assert.NotEmpty(type.GetCustomAttributes<AuthorizeAttribute>());Assert.NotNull(type.GetCustomAttribute<MfaCategoryAttribute>());
        Assert.Equal(PlatformPermissions.Coaching.View,type.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
        foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttributes<HttpMethodAttribute>().Any(a=>!a.HttpMethods.Contains("GET"))))
            Assert.Equal(PlatformPermissions.Coaching.ContentManage,method.GetCustomAttribute<HasPermissionAttribute>()!.Permission);
    }

    [Fact]
    public async Task ControllerCrudReturnsDocumentsAndSafeErrors_WithoutLeakingDatabaseDetails()
    {
        await using var f=new CurriculumManagementTests.Fixture();var controller=new CoachingCurriculumManagementAdminController(f.Service);
        var created=Read(await controller.Create("groups",new("Grup","Grup oluştur"),default));
        Assert.IsType<OkObjectResult>(await controller.Children(ct:default));Assert.IsType<OkObjectResult>(await controller.Get("groups",created.Id,default));
        var changed=Read(await controller.Edit("groups",created.Id,new("Yeni ad","Grubu düzelt",Fingerprint:created.Fingerprint),default));
        Assert.Equal(409,Assert.IsAssignableFrom<ObjectResult>(await controller.Edit("groups",changed.Id,new("Eski form","Eski form kaydet",Fingerprint:created.Fingerprint),default)).StatusCode);
        changed=Read(await controller.Status("groups",changed.Id,new(changed.Fingerprint,"Grubu pasife al",false),default));
        Assert.IsType<BadRequestObjectResult>(await controller.Move("groups",changed.Id,new(null,changed.Fingerprint,"Grubu taşımayı dene"),default));
        Assert.IsType<BadRequestObjectResult>(await controller.Delete("groups",changed.Id,new(changed.Fingerprint,"Grubu silmeyi dene",Guid.NewGuid()),default));
        Assert.IsType<OkObjectResult>(await controller.Delete("groups",changed.Id,new(changed.Fingerprint,"Kullanılmayan grubu sil",changed.Id),default));
        Assert.IsType<NotFoundObjectResult>(await controller.Get("groups",changed.Id,default));
        f.Scope.Global=false;var denied=Assert.IsType<ObjectResult>(await controller.Children(ct:default));Assert.Equal(403,denied.StatusCode);
    }

    [Fact]
    public async Task DurationControllerPreservesUsedEstimateAndCanWithdrawOrDeleteUnusedVersion()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();var controller=new CoachingCurriculumManagementAdminController(f.Service);
        var outcome=await f.Db.Set<Coaching.Domain.Entities.StudyCatalogOutcome>().SingleAsync();
        var estimate=await f.Db.Set<Coaching.Domain.Entities.OutcomeDurationEstimate>().SingleAsync();
        var doc=await f.Service.GetAsync("outcomes",outcome.Id,default);
        Assert.Equal(409,Assert.IsAssignableFrom<ObjectResult>(await controller.DeleteEstimate(outcome.Id,estimate.Id,new(doc.Fingerprint,"Kullanılan süreyi sil",estimate.Id),default)).StatusCode);
        doc=Read(await controller.Withdraw(outcome.Id,estimate.Id,new(doc.Fingerprint,"Süreyi yayından kaldır",false),default));
        var unused=await new CoachingOutcomeCatalogService(f.Db,f.Scope,f.User).EstimateAsync(outcome.Id,new(10,15,5,"Yeni süre gözlemi","Low","Yeni sürüm oluştur"),default);
        doc=await f.Service.GetAsync("outcomes",outcome.Id,default);
        Assert.IsType<OkObjectResult>(await controller.DeleteEstimate(outcome.Id,unused.Id,new(doc.Fingerprint,"Kullanılmayan sürümü sil",unused.Id),default));
    }
    private static CurriculumNode Read(IActionResult result)
    { var value=Assert.IsType<OkObjectResult>(result).Value!;return Assert.IsType<CurriculumNode>(value.GetType().GetProperty("data")!.GetValue(value)); }
}
