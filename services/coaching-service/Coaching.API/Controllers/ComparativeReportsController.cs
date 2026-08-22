using Coaching.Application.Queries.GetInstitutionCoachingComparison;
using Coaching.Application.Queries.GetInstitutionEarlyWarnings;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
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

    [HttpGet("institution/{institutionId:guid}/early-warnings")]
    [ProducesResponseType(typeof(InstitutionEarlyWarningReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<InstitutionEarlyWarningReportDto>> GetInstitutionEarlyWarnings(
        Guid institutionId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int? gradeLevel,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await mediator.Send(
                new GetInstitutionEarlyWarningsQuery(
                    institutionId,
                    fromDate,
                    toDate,
                    gradeLevel,
                    pageNumber,
                    pageSize),
                cancellationToken));
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
    }
}
