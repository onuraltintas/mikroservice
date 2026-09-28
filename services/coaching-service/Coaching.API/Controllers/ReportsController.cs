using Coaching.Application.Queries.GetStudentProgress;
using Coaching.Application.Queries.GetTeacherStudentHistory;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using EduPlatform.Shared.Kernel.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("student/{studentId:guid}/progress")]
    [ProducesResponseType(typeof(StudentProgressSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<StudentProgressSummaryDto>> GetStudentProgress(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(new GetStudentProgressQuery(studentId), cancellationToken));
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }

    [HttpGet("student/{studentId:guid}/history")]
    [ProducesResponseType(typeof(PagedResponse<CoachingAdminStudentHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetStudentHistory(
        Guid studentId,
        [FromQuery] CoachingStudentHistoryType type = CoachingStudentHistoryType.Assignments,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await mediator.Send(
                new GetTeacherStudentHistoryQuery(studentId, type, pageNumber, pageSize)
                { Filter = new CoachingStudentHistoryFilter(fromDate, toDate, status, search) },
                cancellationToken));
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
