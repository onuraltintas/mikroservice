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
public sealed class CoachingCatalogDeletionAdminController(ICoachingCatalogDeletionService service) : ControllerBase
{
    [HttpGet("{kind}/{id:guid}/usage")]
    public async Task<IActionResult> Usage(string kind, Guid id, CancellationToken cancellationToken)
    {
        if (!TryKind(kind, out var parsed)) return InvalidKind();
        try { return Ok(new { success = true, data = await service.GetUsageAsync(parsed, id, cancellationToken) }); }
        catch (BusinessRuleException exception) when (exception.Code == "Catalog.NotFound")
        { return NotFound(new { success = false, message = exception.Message }); }
    }

    [HttpDelete("{kind}/{id:guid}")]
    [HasPermission(PlatformPermissions.Coaching.ContentManage)]
    public async Task<IActionResult> Delete(string kind, Guid id, [FromBody] CatalogDeleteRequest request, CancellationToken cancellationToken)
    {
        if (!TryKind(kind, out var parsed)) return InvalidKind();
        try
        {
            await service.DeleteAsync(parsed, id, request, cancellationToken);
            return Ok(new { success = true, message = "Kullanılmayan katalog kaydı kalıcı olarak silindi." });
        }
        catch (ArgumentException exception)
        { return BadRequest(new { success = false, message = exception.Message }); }
        catch (BusinessRuleException exception) when (exception.Code == "Catalog.NotFound")
        { return NotFound(new { success = false, message = exception.Message }); }
        catch (BusinessRuleException exception) when (exception.Code is "Catalog.InUse" or "Catalog.Stale")
        { return Conflict(new { success = false, message = exception.Message }); }
        catch (PostgresException exception) when (exception.SqlState is "55P03" or "40P01")
        { return Conflict(new { success = false, message = "Kayıtlar şu anda işleniyor. Biraz sonra yeniden deneyin." }); }
    }

    private BadRequestObjectResult InvalidKind() => BadRequest(new { success = false, message = "Geçerli bir katalog türü seçin." });
    private static bool TryKind(string kind, out CatalogKind parsed) => Enum.TryParse(kind, true, out parsed)
        && Enum.IsDefined(parsed) && string.Equals(kind, parsed.ToString(), StringComparison.OrdinalIgnoreCase);
}
