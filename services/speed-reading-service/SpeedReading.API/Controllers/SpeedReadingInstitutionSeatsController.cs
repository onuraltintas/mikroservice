using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Contracts.Billing;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Institutions;
using SpeedReading.Application.Subscription;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.API.Controllers;

/// <summary>
/// The seats of an institution's Hızlı Okuma plan (billing stage 7). The plan and its seat count are bought in the
/// billing service; here the institution's admin says which of its students sit. A student without a seat has no
/// access through the institution.
/// </summary>
[ApiController]
[Route("api/speed-reading/institutions/{institutionId:guid}/seats")]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.Institutions.Manage)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class SpeedReadingInstitutionSeatsController(
    ISpeedReadingBillingSeats seats,
    ISpeedReadingBillingAccess access,
    ISpeedReadingInstitutionMemberships memberships,
    ISpeedReadingInstitutionAdministrationAuthorization administrationAuthorization,
    ILogger<SpeedReadingInstitutionSeatsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid institutionId, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        if (!seats.Enabled) return Ok(new InstitutionSeatsResponse(false, null));
        try
        {
            return Ok(new InstitutionSeatsResponse(true, await seats.GetPoolAsync(institutionId, cancellationToken)));
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception);
        }
    }

    [HttpPut("{studentId:guid}")]
    public async Task<IActionResult> Assign(Guid institutionId, Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId) || !await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        if (!seats.Enabled) return NotFound(new { code = "Billing.NotEnabled", message = "Bu kurulumda koltuk yönetimi açık değil." });
        // Billing only counts seats; that the user is an active student of this institution is checked here.
        var member = await memberships.GetMembersAsync(institutionId, 1, 1, cancellationToken,
            role: SpeedReadingInstitutionMemberRole.Student, isActive: true, memberUserId: studentId);
        if (member.Items.Count == 0)
            return NotFound(new { code = "Institution.StudentNotFound", message = "Bu kurumun etkin öğrencileri arasında böyle bir öğrenci yok." });
        try
        {
            var result = await seats.AssignAsync(institutionId, studentId, actorId, cancellationToken);
            if (!result.Done)
                return Conflict(result.Code == "PoolFull"
                    ? new { code = "Seats.PoolFull", message = "Boş koltuk yok. Bir koltuğu geri alın ya da Aboneliğim sayfasından ek koltuk alın." }
                    : new { code = "Seats.NoPool", message = "Kurumun süren bir Hızlı Okuma planı yok." });
            access.Forget(studentId);
            return Ok(new InstitutionSeatsResponse(true, result.Pool));
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception);
        }
    }

    [HttpDelete("{studentId:guid}")]
    public async Task<IActionResult> Release(Guid institutionId, Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId) || !await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        if (!seats.Enabled) return NotFound(new { code = "Billing.NotEnabled", message = "Bu kurulumda koltuk yönetimi açık değil." });
        try
        {
            // Somebody who left the institution can still be taken off a seat: membership is not asked here.
            var result = await seats.ReleaseAsync(institutionId, studentId, actorId, null, cancellationToken);
            if (!result.Done) return NotFound(new { code = "Seats.NotAssigned", message = "Bu öğrencinin koltuğu yok." });
            access.Forget(studentId);
            return Ok(new InstitutionSeatsResponse(true, result.Pool));
        }
        catch (HttpRequestException exception)
        {
            return Unavailable(exception);
        }
    }

    private ObjectResult Unavailable(Exception exception)
    {
        logger.LogError(exception, "The billing service could not be reached for an institution's seats.");
        return StatusCode(StatusCodes.Status503ServiceUnavailable,
            new { code = "Billing.Unavailable", message = "Abonelik servisine şu anda ulaşılamıyor. Lütfen biraz sonra tekrar deneyin." });
    }

    private async Task<bool> CanManageAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        if (institutionId == Guid.Empty || !TryGetActor(out var actorId)) return false;
        if (User.IsInRole("SystemAdmin")) return true;
        return (User.IsInRole("InstitutionAdmin") || User.IsInRole("InstitutionOwner"))
            && await administrationAuthorization.CanManageAsync(actorId, institutionId, cancellationToken);
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}

/// <summary><paramref name="Enabled"/> false: this deployment runs without the billing service. <paramref name="Pool"/> null: no running plan with seats.</summary>
public sealed record InstitutionSeatsResponse(bool Enabled, BillingSeatPoolResponse? Pool);
