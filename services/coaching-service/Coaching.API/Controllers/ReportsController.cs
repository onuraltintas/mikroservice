using Coaching.Application.Queries.GetStudentProgress;
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
}
