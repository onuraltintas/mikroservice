using Asp.Versioning;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.API.Controllers;

/// <summary>KM7: bulk status, quality report and inline minute/weight edits of the curriculum (global administrator).</summary>
[ApiController, ApiVersion(1.0), Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/curriculum-management")]
public sealed class CoachingCurriculumToolsAdminController(CoachingCatalogAdminToolsService tools) : ControllerBase
{
    [HttpPost("bulk/status"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(65536)]
    public Task<IActionResult> BulkStatus(BulkStatusRequest request, CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.BulkStatusAsync(request, ct));

    [HttpPatch("topics/bulk"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(65536)]
    public Task<IActionResult> PatchTopics(TopicPatchRequest request, CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.PatchTopicsAsync(request, ct));

    [HttpGet("quality")]
    public Task<IActionResult> Quality(string? examCode = null, Guid? lessonId = null, string? issue = null, int page = 1, int pageSize = 50, CancellationToken ct = default)
        => CatalogAdminResult.Execute(async () => await tools.QualityAsync(examCode, lessonId, issue, page, pageSize, ct));

    [HttpGet("quality/export.csv")]
    public async Task<IActionResult> QualityCsv(string? examCode = null, Guid? lessonId = null, string? issue = null, CancellationToken ct = default)
    {
        try { return File(await tools.QualityCsvAsync(examCode, lessonId, issue, ct), "text/csv; charset=utf-8", "katalog-eksikleri.csv"); }
        catch (Exception e) when (e is BusinessRuleException or ArgumentException) { return await CatalogAdminResult.Execute(() => throw e); }
    }
}

/// <summary>KM7: the topic–outcome link review queue, outcome sources and the topics an outcome is linked to.</summary>
[ApiController, ApiVersion(1.0), Authorize]
[HasPermission(PlatformPermissions.Coaching.ContentManage)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/outcomes")]
public sealed class CoachingOutcomeLinksAdminController(CoachingCatalogAdminToolsService tools) : ControllerBase
{
    [HttpGet("mappings")]
    public Task<IActionResult> Mappings(string? status = "pending", string? examCode = null, Guid? lessonId = null, string? source = null, string? search = null,
        int page = 1, int pageSize = 50, CancellationToken ct = default)
        => CatalogAdminResult.Execute(async () => await tools.MappingsAsync(status, examCode, lessonId, source, search, page, pageSize, ct));

    [HttpGet("mappings/sources")]
    public Task<IActionResult> MappingSources(CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.MappingSourcesAsync(ct));

    [HttpPost("mappings/bulk"), RequestSizeLimit(131072)]
    public Task<IActionResult> BulkReview(MappingBulkRequest request, CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.BulkReviewAsync(request, ct));

    [HttpDelete("mappings/{topic:guid}/{outcome:guid}"), RequestSizeLimit(4096)]
    public Task<IActionResult> DeleteMapping(Guid topic, Guid outcome, [FromBody] CatalogReasonRequest request, CancellationToken ct)
        => CatalogAdminResult.Execute(async () => { await tools.DeleteMappingAsync(topic, outcome, request, ct); return new { deleted = true }; });

    [HttpGet("{outcome:guid}/topics")]
    public Task<IActionResult> Topics(Guid outcome, CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.OutcomeTopicsAsync(outcome, ct));

    [HttpGet("{outcome:guid}/source")]
    public Task<IActionResult> Source(Guid outcome, CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.OutcomeSourceAsync(outcome, ct));

    [HttpPut("{outcome:guid}/source"), RequestSizeLimit(16384)]
    public Task<IActionResult> SaveSource(Guid outcome, OutcomeSourceRequest request, CancellationToken ct)
        => CatalogAdminResult.Execute(async () => await tools.SaveOutcomeSourceAsync(outcome, request, ct));
}

/// <summary>KM7: YKS scopes of a topic and the known YKS test codes.</summary>
[ApiController, ApiVersion(1.0), Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/catalog")]
public sealed class CoachingYksScopeAdminController(CoachingCatalogAdminToolsService tools) : ControllerBase
{
    [HttpGet("yks-test-codes")]
    public Task<IActionResult> TestCodes(CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.TestCodesAsync(ct));

    [HttpGet("topics/{topic:guid}/yks-scopes")]
    public Task<IActionResult> Scopes(Guid topic, CancellationToken ct) => CatalogAdminResult.Execute(async () => await tools.YksScopesAsync(topic, ct));

    [HttpPut("topics/{topic:guid}/yks-scopes"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(65536)]
    public Task<IActionResult> SaveScopes(Guid topic, YksScopeSaveRequest request, CancellationToken ct)
        => CatalogAdminResult.Execute(async () => await tools.SaveYksScopesAsync(topic, request, ct));
}

/// <summary>KM7: the university program catalog.</summary>
[ApiController, ApiVersion(1.0), Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/catalog/university-programs")]
public sealed class CoachingUniversityProgramsAdminController(CoachingUniversityProgramAdminService programs) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(string? search = null, string? scoreType = null, string? city = null, int? year = null, bool? active = null,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
        => CatalogAdminResult.Execute(async () => await programs.ListAsync(search, scoreType, city, year, active, page, pageSize, ct));

    [HttpGet("{id:guid}")]
    public Task<IActionResult> Get(Guid id, CancellationToken ct) => CatalogAdminResult.Execute(async () => await programs.GetAsync(id, ct));

    [HttpPost, HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(16384)]
    public Task<IActionResult> Create(UniversityProgramSaveRequest request, CancellationToken ct) => CatalogAdminResult.Execute(async () => await programs.CreateAsync(request, ct));

    [HttpPut("{id:guid}"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(16384)]
    public Task<IActionResult> Update(Guid id, UniversityProgramSaveRequest request, CancellationToken ct) => CatalogAdminResult.Execute(async () => await programs.UpdateAsync(id, request, ct));

    [HttpPatch("{id:guid}/status"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> Status(Guid id, UniversityProgramStatusRequest request, CancellationToken ct) => CatalogAdminResult.Execute(async () => await programs.SetActiveAsync(id, request, ct));

    [HttpDelete("{id:guid}"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> Delete(Guid id, [FromBody] UniversityProgramDeleteRequest request, CancellationToken ct)
        => CatalogAdminResult.Execute(async () => { await programs.DeleteAsync(id, request, ct); return new { deleted = true }; });
}

/// <summary>KM7: the shared {success, data} / Turkish error mapping of the catalog administration endpoints.</summary>
internal static class CatalogAdminResult
{
    public static async Task<IActionResult> Execute(Func<Task<object>> operation)
    {
        try { return new OkObjectResult(new { success = true, data = await operation() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.")) { return new ObjectResult(new { success = false, message = e.Message }) { StatusCode = 403 }; }
        catch (BusinessRuleException e) { return new ConflictObjectResult(new { success = false, message = e.Message }); }
        catch (KeyNotFoundException e) { return new NotFoundObjectResult(new { success = false, message = e.Message }); }
        catch (ArgumentException e) { return new BadRequestObjectResult(new { success = false, message = Turkish(e.Message) }); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23503" or "23505" })
        { return new ConflictObjectResult(new { success = false, message = "Kayıt kullanımda veya aynı kod zaten var. Güncel kaydı yeniden açın." }); }
        catch (PostgresException e) when (e.SqlState is "55P03" or "40P01")
        { return new ConflictObjectResult(new { success = false, message = "Katalog başka bir işlemde kullanılıyor. Yeniden deneyin." }); }
    }

    // Domain validation of older entities is in English; admins see Turkish only.
    private static string Turkish(string message) => message.StartsWith("Catalog text must", StringComparison.Ordinal)
        ? "Metin alanlarını kontrol edin (boş olamaz, uzunluk sınırını aşamaz)."
        : message.Contains(" (Parameter", StringComparison.Ordinal) ? "Girilen değerleri kontrol edin." : message;
}
