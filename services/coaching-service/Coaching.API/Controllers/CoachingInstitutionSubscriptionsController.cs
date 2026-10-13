using Asp.Versioning;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "InstitutionAdmin,InstitutionOwner")]
[Route("api/coaching/institution-subscription")]
public sealed class CoachingInstitutionSubscriptionsController(CoachingInstitutionSubscriptionService subscriptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        try { return Ok(await subscriptions.GetAsync(ct)); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Authorization.")) { return StatusCode(403, new { error = error.Message }); }
    }
    [HttpPut("{subscriptionId:guid}/students/{studentId:guid}")]
    [MfaCategory(MfaOperationCategories.Coaching)]
    [RequestSizeLimit(4_096)]
    public async Task<IActionResult> SetSeat(Guid subscriptionId, Guid studentId, [FromBody] InstitutionSeatRequest request, CancellationToken ct)
    {
        try { return await subscriptions.SetSeatAsync(subscriptionId, studentId, request.IsSuspended, ct)
            ? NoContent() : Conflict(new { error = "Etkin lisans, boş kontenjan veya kayıt bulunamadı. Bilgileri yenileyin." }); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Authorization.")) { return StatusCode(403, new { error = error.Message }); }
    }
}
public sealed record InstitutionSeatRequest(bool IsSuspended);
