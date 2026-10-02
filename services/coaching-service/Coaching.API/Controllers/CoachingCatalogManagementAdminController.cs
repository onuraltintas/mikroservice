using System.Text.Json;
using Coaching.Application.CatalogAdministration;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/catalog")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingCatalogManagementAdminController(ICoachingCatalogManagementService service) : ControllerBase
{
    [HttpGet("{kind}/{id:guid}")]
    public Task<IActionResult> Get(string kind, Guid id, CancellationToken ct)
        => Execute(kind, parsed => service.GetAsync(parsed, id, ct), ct);

    [HttpPost("{kind}")]
    [HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public Task<IActionResult> Create(string kind, [FromBody] CatalogSaveRequest request, CancellationToken ct)
        => Execute(kind, parsed => service.CreateAsync(parsed, request, ct), ct, true);

    [HttpPut("{kind}/{id:guid}")]
    [HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public Task<IActionResult> Update(string kind, Guid id, [FromBody] CatalogSaveRequest request, CancellationToken ct)
        => Execute(kind, parsed => service.UpdateAsync(parsed, id, request, ct), ct);

    [HttpPatch("{kind}/{id:guid}/status")]
    [HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public Task<IActionResult> SetActive(string kind, Guid id, [FromBody] CatalogStatusRequest request, CancellationToken ct)
        => Execute(kind, parsed => service.SetActiveAsync(parsed, id, request, ct), ct);

    private async Task<IActionResult> Execute(string kind, Func<CatalogKind, Task<CatalogEditDocument>> operation,
        CancellationToken ct, bool created = false)
    {
        if (!Enum.TryParse<CatalogKind>(kind, true, out var parsed) || !Enum.IsDefined(parsed)
            || !string.Equals(kind, parsed.ToString(), StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "Geçerli bir katalog türü seçin." });
        try
        {
            var document = await operation(parsed);
            // Expose the existing camelCase catalog DTO, not domain serialization conventions.
            var row = JsonSerializer.Deserialize<AdminCatalogRow>(document.Data.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            var response = new { success = true, data = new { document.Fingerprint, data = row } };
            return created ? Created($"/api/coaching-admin/catalog/{kind}/{row.Id}", response) : Ok(response);
        }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Katalog alanlarını, işlem gerekçesini ve bağlantıları kontrol edin." }); }
        catch (BusinessRuleException ex) when (ex.Code == "Catalog.NotFound")
        { return NotFound(new { success = false, message = ex.Message }); }
        catch (BusinessRuleException ex) when (ex.Code is "Catalog.Stale" or "Catalog.Hierarchy")
        { return Conflict(new { success = false, message = ex.Message }); }
        catch (PostgresException ex) when (ex.SqlState is "55P03" or "40P01")
        { return Conflict(new { success = false, message = "Katalog işleniyor. Biraz sonra yeniden deneyin." }); }
        catch (HttpRequestException)
        { return StatusCode(503, new { success = false, message = "Ortak konum dizinine şu anda erişilemiyor. Daha sonra yeniden deneyin." }); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { return StatusCode(503, new { success = false, message = "Ortak konum dizini zamanında yanıt vermedi. Yeniden deneyin." }); }
    }
}
