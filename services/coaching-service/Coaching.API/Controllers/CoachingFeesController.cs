using Coaching.Application.Finance;
using Coaching.Infrastructure.Finance;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize, Route("api/coaching/finance")]
public sealed class CoachingFeesController(CoachingFeeService fees, ICurrentUserService actor) : CoachingFinanceControllerBase
{
    private const string StaffRoles = "Teacher,InstitutionAdmin,InstitutionOwner";
    [HttpGet("receivables"), Authorize(Roles = StaffRoles)]
    public Task<IActionResult> Receivables(int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetReceivablesAsync(pageNumber, pageSize, ct) }));
    [HttpGet("me"), Authorize(Roles = "Student")]
    public Task<IActionResult> MyAccount(int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetStudentAsync(actor.UserId ?? Guid.Empty, pageNumber, pageSize, ct, studentView: true) }));
    [HttpGet("me/fees/{feeId:guid}"), Authorize(Roles = "Student")]
    public Task<IActionResult> MyFee(Guid feeId, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetAsync(actor.UserId ?? Guid.Empty, feeId, ct, studentView: true) }));
    [HttpGet("me/fees/{feeId:guid}/payments"), Authorize(Roles = "Student")]
    public Task<IActionResult> MyPayments(Guid feeId, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetPaymentsAsync(actor.UserId ?? Guid.Empty, feeId, pageNumber, pageSize, ct, studentView: true) }));
    [HttpGet("students/{studentId:guid}"), Authorize(Roles = StaffRoles)]
    public Task<IActionResult> Account(Guid studentId, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetStudentAsync(studentId, pageNumber, pageSize, ct) }));
    [HttpGet("students/{studentId:guid}/fees/{feeId:guid}"), Authorize(Roles = StaffRoles)]
    public Task<IActionResult> Fee(Guid studentId, Guid feeId, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetAsync(studentId, feeId, ct) }));
    [HttpGet("students/{studentId:guid}/fees/{feeId:guid}/payments"), Authorize(Roles = StaffRoles)]
    public Task<IActionResult> Payments(Guid studentId, Guid feeId, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetPaymentsAsync(studentId, feeId, pageNumber, pageSize, ct) }));
    [HttpGet("cash"), Authorize(Roles = StaffRoles)]
    public Task<IActionResult> Cash([FromQuery, BindRequired] DateOnly fromDate, [FromQuery, BindRequired] DateOnly toDate,
        int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetCashAsync(fromDate, toDate, pageNumber, pageSize, ct) }));

    [HttpPost("students/{studentId:guid}/fees"), Authorize(Roles = StaffRoles), MfaCategory(MfaOperationCategories.Coaching),
     RequestSizeLimit(16384), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Create(Guid studentId, CoachingFeeCreate request, CancellationToken ct) => Respond(async () => {
        var fee = await fees.CreateAsync(studentId, request, ct);
        return CreatedAtAction(nameof(Fee), new { studentId, feeId = fee.Id }, new { success = true, data = fee });
    });
    [HttpPost("students/{studentId:guid}/fees/{feeId:guid}/payments"), Authorize(Roles = StaffRoles), MfaCategory(MfaOperationCategories.Coaching),
     RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Pay(Guid studentId, Guid feeId, CoachingFeePaymentInput request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await fees.PayAsync(studentId, feeId, request, ct) }));
    [HttpPost("students/{studentId:guid}/fees/{feeId:guid}/payments/{paymentId:guid}/reverse"), Authorize(Roles = StaffRoles), MfaCategory(MfaOperationCategories.Coaching),
     RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Reverse(Guid studentId, Guid feeId, Guid paymentId, CoachingFeeReversalInput request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await fees.ReverseAsync(studentId, feeId, paymentId, request, ct) }));
    [HttpPost("students/{studentId:guid}/fees/{feeId:guid}/cancel"), Authorize(Roles = StaffRoles), MfaCategory(MfaOperationCategories.Coaching),
     RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Cancel(Guid studentId, Guid feeId, CoachingFeeCancelInput request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await fees.CancelAsync(studentId, feeId, request, ct) }));
}

[ApiController, ApiVersion(1.0), Route("api/coaching-admin/finance"), Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Coaching.View), MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingAdminFeesController(CoachingFeeService fees) : CoachingFinanceControllerBase
{
    [HttpGet("receivables")]
    public Task<IActionResult> Receivables(int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetReceivablesAsync(pageNumber, pageSize, ct) }));
    [HttpGet("students/{studentId:guid}")]
    public Task<IActionResult> Account(Guid studentId, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetStudentAsync(studentId, pageNumber, pageSize, ct) }));
    [HttpGet("students/{studentId:guid}/fees/{feeId:guid}/payments")]
    public Task<IActionResult> Payments(Guid studentId, Guid feeId, int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetPaymentsAsync(studentId, feeId, pageNumber, pageSize, ct) }));
    [HttpGet("cash")]
    public Task<IActionResult> Cash([FromQuery, BindRequired] DateOnly fromDate, [FromQuery, BindRequired] DateOnly toDate,
        int pageNumber = 1, int pageSize = 25, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await fees.GetCashAsync(fromDate, toDate, pageNumber, pageSize, ct) }));
}

public abstract class CoachingFinanceControllerBase : ControllerBase
{
    protected async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (ArgumentException) { return BadRequest(new { success = false, message = "Ücret, taksit, tahsilat, tarih ve işlem kimliği alanlarını kontrol edin." }); }
        catch (BusinessRuleException error) when (error.Code == "Authorization.Forbidden") { return StatusCode(403, new { success = false, message = error.Message, code = error.Code }); }
        catch (BusinessRuleException error) when (error.Code == "Finance.NotFound") { return NotFound(new { success = false, message = error.Message, code = error.Code }); }
        catch (BusinessRuleException error) when (error.Code == "Finance.Conflict") { return Conflict(new { success = false, message = error.Message, code = error.Code }); }
        catch (ConcurrencyException) { return Conflict(new { success = false, message = "Ücret kaydı değişti. Güncel bilgileri yükleyin.", code = "Finance.Conflict" }); }
    }
}
