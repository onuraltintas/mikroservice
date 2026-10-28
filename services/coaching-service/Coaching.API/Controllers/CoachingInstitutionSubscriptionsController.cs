using Asp.Versioning;
using Coaching.Application.Subscriptions;
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
public sealed class CoachingInstitutionSubscriptionsController(CoachingInstitutionSubscriptionService subscriptions, CoachingSeatRequestService seatRequests) : ControllerBase
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

    // ---- Roadmap O10b: extra seats on a running license -----------------------------------------------------------

    [HttpGet("{subscriptionId:guid}/seat-quote")]
    public Task<IActionResult> SeatQuote(Guid subscriptionId, [FromQuery] int seats, CancellationToken ct) =>
        Handle(async () => Ok(await seatRequests.QuoteAsync(subscriptionId, seats, ct)));

    [HttpGet("seat-requests")]
    public Task<IActionResult> SeatRequests(CancellationToken ct) => Handle(async () => Ok(await seatRequests.ListMineAsync(ct)));

    [HttpPost("seat-requests")]
    [MfaCategory(MfaOperationCategories.Coaching)]
    [RequestSizeLimit(4_096)]
    public Task<IActionResult> CreateSeatRequest([FromBody] CoachingSeatRequestCreate request, CancellationToken ct) =>
        Handle(async () => Ok(await seatRequests.CreateAsync(request, ct)));

    [HttpPost("seat-requests/{id:guid}/withdraw")]
    [MfaCategory(MfaOperationCategories.Coaching)]
    public Task<IActionResult> WithdrawSeatRequest(Guid id, CancellationToken ct) => Handle(async () => Ok(await seatRequests.WithdrawAsync(id, ct)));

    private async Task<IActionResult> Handle(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Authorization.")) { return StatusCode(403, new { error = error.Message, message = error.Message }); }
        catch (BusinessRuleException error) { return Conflict(new { code = error.Code, error = error.Message, message = error.Message }); }
        catch (KeyNotFoundException) { return NotFound(new { error = "Etkin lisans ya da talep bulunamadı.", message = "Etkin lisans ya da talep bulunamadı." }); }
        catch (ArgumentException error) { return BadRequest(new { error = error.Message, message = error.Message }); }
    }
}
public sealed record InstitutionSeatRequest(bool IsSuspended);
