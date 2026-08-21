using Coaching.Application.Queries.GetInstitutionCoachingComparison;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Coaching.View)]
[Route("api/reports")]
[Produces("application/json")]
public sealed class ComparativeReportsController(IMediator mediator) : ControllerBase
{
    [HttpGet("institution/{institutionId:guid}/comparison")]
    [ProducesResponseType(typeof(InstitutionCoachingComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InstitutionCoachingComparisonDto>> GetInstitutionComparison(
        Guid institutionId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? gradeLevel,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(
                new GetInstitutionCoachingComparisonQuery(
                    institutionId,
                    fromDate,
                    toDate,
                    gradeLevel),
                cancellationToken));
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
