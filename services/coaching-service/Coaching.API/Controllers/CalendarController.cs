using Coaching.Domain;
using System.Text;
using Coaching.Application.Authorization;
using Coaching.Application.Queries.GetCalendarFeed;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/calendar")]
public sealed class CalendarController(
    IMediator mediator,
    ICoachingAccessPolicy accessPolicy,
    Coaching.Infrastructure.Management.CoachingCalendarSubscriptionService subscriptions) : ControllerBase
{
    // ---- Roadmap O6: private calendar link for Google / Apple Calendar ------------------------------------------------

    [HttpGet("subscription")]
    public async Task<IActionResult> GetSubscription([FromQuery] string role, CancellationToken cancellationToken) =>
        await ForRole(role, async userId => Ok(await subscriptions.GetAsync(userId, role, cancellationToken)));

    /// <summary>Creates a new link (the previous one stops working); the path is returned only now.</summary>
    [HttpPost("subscription")]
    public async Task<IActionResult> CreateSubscription([FromQuery] string role, CancellationToken cancellationToken) =>
        await ForRole(role, async userId => Ok(await subscriptions.CreateAsync(userId, role, cancellationToken)));

    [HttpDelete("subscription")]
    public async Task<IActionResult> RevokeSubscription([FromQuery] string role, CancellationToken cancellationToken) =>
        await ForRole(role, async userId => { await subscriptions.RevokeAsync(userId, role, cancellationToken); return NoContent(); });

    /// <summary>The calendar a subscribed app downloads. The secret token in the link is the only credential.</summary>
    [HttpGet("feed/{token}")]
    [HttpHead("feed/{token}")] // Roadmap F1: calendar apps may check the link with HEAD; the gateway already lets it through.
    [AllowAnonymous]
    [Produces("text/calendar")]
    public async Task<IActionResult> Feed(string token, CancellationToken cancellationToken)
    {
        var content = await subscriptions.FeedAsync(token, cancellationToken);
        if (content is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=900";
        Response.Headers["X-Robots-Tag"] = "noindex";
        return File(Encoding.UTF8.GetBytes(content), "text/calendar; charset=utf-8");
    }

    private async Task<IActionResult> ForRole(string? role, Func<Guid, Task<IActionResult>> action)
    {
        var userId = accessPolicy.CurrentUserId;
        if (!userId.HasValue) return Unauthorized();
        var allowed = role?.Trim().ToLowerInvariant() switch
        {
            "teacher" => User.IsInRole("Teacher"),
            "student" => User.IsInRole("Student"),
            _ => false
        };
        if (!allowed) return StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu rol için takvim bağlantısı oluşturulamaz." });
        return await action(userId.Value);
    }

    [HttpGet("teacher.ics")]
    [Produces("text/calendar")]
    public async Task<IActionResult> GetTeacherCalendar(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var teacherId = accessPolicy.CurrentUserId;
        if (!teacherId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var feed = await mediator.Send(
                new GetTeacherCalendarFeedQuery(teacherId.Value, CoachingTime.RangeStart(fromDate), CoachingTime.RangeEnd(toDate)),
                cancellationToken);
            return File(
                Encoding.UTF8.GetBytes(feed.Content),
                feed.ContentType,
                "coaching-teacher.ics");
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Validation.", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (EduPlatform.Shared.Kernel.Exceptions.ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpGet("student.ics")]
    [Produces("text/calendar")]
    public async Task<IActionResult> GetStudentCalendar(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken)
    {
        var studentId = accessPolicy.CurrentUserId;
        if (!studentId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var feed = await mediator.Send(
                new GetStudentCalendarFeedQuery(studentId.Value, CoachingTime.RangeStart(fromDate), CoachingTime.RangeEnd(toDate)),
                cancellationToken);
            return File(
                Encoding.UTF8.GetBytes(feed.Content),
                feed.ContentType,
                "coaching-student.ics");
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Validation.", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (EduPlatform.Shared.Kernel.Exceptions.ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }
}
