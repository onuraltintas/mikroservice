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
    ICoachingAccessPolicy accessPolicy) : ControllerBase
{
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
                new GetTeacherCalendarFeedQuery(teacherId.Value, fromDate, toDate),
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
                new GetStudentCalendarFeedQuery(studentId.Value, fromDate, toDate),
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
