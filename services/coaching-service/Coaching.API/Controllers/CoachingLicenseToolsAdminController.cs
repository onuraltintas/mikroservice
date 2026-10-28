using System.Security.Claims;
using Asp.Versioning;
using Coaching.API.Security;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

/// <summary>Roadmap D6: license end date and seats, ending / ended licenses, Excel payments export and churn report.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/subscriptions")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.SubscriptionManage)]
[RequireGlobalCoachingAdmin]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingLicenseToolsAdminController(CoachingAdminToolsService tools) : ControllerBase
{
    [HttpPut("{id:guid}/license")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> UpdateLicense(Guid id, [FromBody] CoachingLicenseChange change, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var actor)) return Unauthorized();
        if (change.EndDate is null && change.SeatCapacity is null) return BadRequest(new { success = false, message = "Bitiş tarihi ya da koltuk sayısı girin." });
        try { return Ok(new { success = true, data = await tools.UpdateLicenseAsync(id, change, actor, ct) }); }
        catch (BusinessRuleException error) when (error.Code == "Subscription.NotFound") { return NotFound(new { success = false, message = error.Message }); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Subscription.", StringComparison.Ordinal))
        { return BadRequest(new { success = false, message = error.Message, code = error.Code }); }
    }

    [HttpGet("window")]
    public async Task<IActionResult> Window([FromQuery] string window = "ending", CancellationToken ct = default)
    {
        try { return Ok(new { success = true, data = await tools.WindowAsync(window, ct) }); }
        catch (ArgumentException error) { return BadRequest(new { success = false, message = error.Message }); }
    }

    [HttpGet("payments/export")]
    public async Task<IActionResult> ExportPayments([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] string? status, CancellationToken ct)
    {
        if (fromDate > toDate) return BadRequest(new { success = false, message = "Başlangıç tarihi bitişten sonra olamaz." });
        var bytes = await tools.ExportPaymentsAsync(fromDate, toDate, status, ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"kocluk-odemeleri-{fromDate?.ToString("yyyyMMdd") ?? "tum"}-{toDate?.ToString("yyyyMMdd") ?? "bugun"}.xlsx");
    }

    [HttpGet("churn")]
    public async Task<IActionResult> Churn([FromQuery] int months = 12, CancellationToken ct = default)
    {
        try { return Ok(new { success = true, data = await tools.ChurnAsync(months, ct) }); }
        catch (ArgumentException error) { return BadRequest(new { success = false, message = error.Message }); }
    }
}
