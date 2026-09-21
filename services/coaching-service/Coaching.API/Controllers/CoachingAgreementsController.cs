using Coaching.Application.CoachingAgreements;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/coaching-agreements")]
[Produces("application/json")]
public sealed class CoachingAgreementsController(IMediator mediator) : ControllerBase
{
    [HttpPost("documents")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    [ProducesResponseType(typeof(PublishCoachingAgreementResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PublishCoachingAgreementResponse>> Publish(
        [FromBody] PublishCoachingAgreementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCurrent), new { locale = command.Locale }, result);
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentCoachingAgreementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CurrentCoachingAgreementResponse>> GetCurrent(
        [FromQuery] string locale = "tr-TR",
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(
            new GetCurrentCoachingAgreementQuery(locale),
            cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("current/acknowledgements")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(CoachingAgreementAcknowledgementResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<CoachingAgreementAcknowledgementResponse>> AcknowledgeCurrent(
        [FromBody] AcknowledgeCurrentCoachingAgreementCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCurrent), new { locale = result.Locale }, result);
    }

    [HttpDelete("acknowledgements/{acknowledgementId:guid}")]
    [Authorize(Roles = "Student")]
    [ProducesResponseType(typeof(CoachingAgreementAcknowledgementResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<CoachingAgreementAcknowledgementResponse>> WithdrawAcknowledgement(
        Guid acknowledgementId,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new WithdrawCoachingAgreementAcknowledgementCommand(acknowledgementId),
            cancellationToken));
}
