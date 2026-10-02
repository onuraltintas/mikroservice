using System.Text;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;

namespace Coaching.API.Controllers;

public sealed record CatalogImportRequest(string Source, Dictionary<string, string> Files, string? Fingerprint = null, string? Reason = null);

[ApiController, ApiVersion(1.0), Route("api/coaching-admin/catalog-imports"), Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[RequestSizeLimit(48 * 1024 * 1024)]
[EnableRateLimiting("study-planning-write")]
public sealed class CoachingCatalogImportAdminController(CoachingDbContext db, ICoachingAdminScopeAuthorization scope,
    ICurrentUserService user) : ControllerBase
{
    [HttpPost("preview"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public Task<IActionResult> Preview(CatalogImportRequest request, CancellationToken ct) => Execute(request, null, ct);

    [HttpPost("approve"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public Task<IActionResult> Import(CatalogImportRequest request, CancellationToken ct) => Execute(request, false, ct);

    [HttpPost("publish"), HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public Task<IActionResult> Publish(CatalogImportRequest request, CancellationToken ct) => Execute(request, true, ct);

    private async Task<IActionResult> Execute(CatalogImportRequest request, bool? publish, CancellationToken ct)
    {
        if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal || user.UserId is not { } actor)
            throw new BusinessRuleException("Authorization.Forbidden", "Ortak katalog aktarımı yalnız global yöneticiye açıktır.");
        if (request.Files is null || request.Files.Count != 6 || request.Files.Any(x => x.Value is null)
            || request.Files.Sum(x => (long)Encoding.UTF8.GetByteCount(x.Value)) > 40 * 1024 * 1024)
            return BadRequest(new { success = false, message = "Altı katalog JSON dosyasını toplam 40 MiB sınırı içinde seçin." });
        try
        {
            var importer = new CoachingCatalogImporter(db);
            if (publish is null) return Ok(new { success = true, data = await importer.ReviewAsync(request.Files, request.Source, ct) });
            var changed = await importer.ApproveAsync(request.Files, request.Source, request.Fingerprint!, request.Reason!, actor, publish.Value, ct);
            return Ok(new { success = true, data = new { changed } });
        }
        catch (Exception ex) when (ex is ArgumentException or JsonException or FormatException or OverflowException)
        { return BadRequest(new { success = false, message = "Dosya alanlarını, ilişkileri, kaynak adını ve işlem gerekçesini kontrol edin." }); }
        catch (InvalidOperationException)
        { return Conflict(new { success = false, message = "Dosyalar mevcut katalogla uyuşmuyor veya önizleme değişti. Silinmiş kayıtları ve değişmiş içeriği kontrol edip yeniden önizleyin." }); }
        catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01")
        { return Conflict(new { success = false, message = "Katalog başka bir işlemde kullanılıyor. Yeniden deneyin." }); }
    }
}
