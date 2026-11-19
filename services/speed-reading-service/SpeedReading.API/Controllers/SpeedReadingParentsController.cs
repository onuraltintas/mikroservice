using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Parents;
using SpeedReading.Application.Subscription;

namespace SpeedReading.API.Controllers;

/// <summary>
/// V2 (parent panel, decision 01.11.2026): a parent reads a child's speed reading. Identity's authorize-read is asked on every
/// request (verified Mother/Father/Guardian link, both active, the child has speed-reading access and left it visible).
/// Parents sign in to the Coaching portal; ProductScopeMiddleware accepts their Coaching token on this route only.
/// </summary>
[ApiController]
[Route("api/speed-reading/parents/me/children")]
[Authorize(Roles = "Parent")]
public sealed class SpeedReadingParentsController(
    ISpeedReadingParentAccess access, ISpeedReadingParentReports reports, ISpeedReadingBillingAccess billing) : ControllerBase
{
    [HttpGet("{studentId:guid}/overview")]
    public async Task<IActionResult> GetOverview(Guid studentId, CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty)
            return NotFound();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var parentUserId)
            || parentUserId == Guid.Empty)
            return Unauthorized();
        if (!await access.CanReadAsync(parentUserId, studentId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "ParentReadForbidden",
                message = "Bu öğrencinin hızlı okuma bilgilerini görme iznin yok."
            });

        return Ok(await reports.GetOverviewAsync(studentId, DateTime.UtcNow, cancellationToken));
    }

    /// <summary>
    /// Live test 19.11.2026 (A15): the parent sees whether the child's Hızlı Okuma needs a subscription and until when
    /// it is open, as on the Koçluk tab. The subscription itself is bought in the student's own account.
    /// </summary>
    [HttpGet("{studentId:guid}/subscription")]
    public async Task<IActionResult> GetSubscription(Guid studentId, CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty)
            return NotFound();
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var parentUserId)
            || parentUserId == Guid.Empty)
            return Unauthorized();
        if (!await access.CanReadAsync(parentUserId, studentId, cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                code = "ParentReadForbidden",
                message = "Bu öğrencinin hızlı okuma bilgilerini görme iznin yok."
            });

        if (!billing.Enabled)
            return Ok(new SpeedReadingParentSubscription(false, true, null, true));
        var decision = await billing.GetAsync(studentId, cancellationToken);
        return Ok(new SpeedReadingParentSubscription(decision.RequireSubscription, decision.Allowed, decision.EndsAt, decision.Known));
    }
}

/// <summary>A15: what the parent panel shows about the child's Hızlı Okuma subscription.</summary>
public sealed record SpeedReadingParentSubscription(bool RequireSubscription, bool HasAccess, DateTime? AccessUntil, bool Known);

/// <summary>V2: the Coaching weekly parent summary reads the speed-reading section here (service key only, never via the gateway).</summary>
[ApiController]
[Route("api/internal/speed-reading")]
public sealed class InternalSpeedReadingParentsController(ISpeedReadingParentReports reports) : ControllerBase
{
    [HttpGet("parent-weekly-summary")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> GetParentWeeklySummary(
        [FromQuery] Guid? studentUserId,
        [FromQuery] string? weekStart,
        CancellationToken cancellationToken)
    {
        if (studentUserId is not { } student || student == Guid.Empty
            || !DateOnly.TryParseExact(weekStart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var week))
            return BadRequest(new { code = "ValidationFailed", message = "studentUserId and weekStart (yyyy-MM-dd) are required." });

        var summary = await reports.GetWeeklySummaryAsync(student, week, DateTime.UtcNow, cancellationToken);
        return summary is null ? NoContent() : Ok(summary);
    }
}
