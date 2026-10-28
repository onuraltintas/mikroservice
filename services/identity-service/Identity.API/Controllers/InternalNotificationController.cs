using Identity.Application.Commands.Notification.ForwardSupportRequest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.API;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/notification")]
public class InternalNotificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalNotificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed record RecipientsRequest(IReadOnlyCollection<Guid>? UserIds);

    /// <summary>Roadmap O8: names and usable e-mail addresses of up to 200 notification recipients.</summary>
    [HttpPost("recipients")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Recipients(
        [FromServices] Identity.Application.Notifications.INotificationRecipientDirectory directory,
        [FromBody] RecipientsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.UserIds is null || request.UserIds.Count > Identity.Application.Notifications.INotificationRecipientDirectory.MaxUsers)
            return BadRequest("The recipient lookup is invalid.");
        return Ok(await directory.GetAsync(request.UserIds, cancellationToken));
    }

    [HttpPost("forward-support")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(32_768)]
    public async Task<IActionResult> ForwardSupport([FromBody] ForwardSupportRequestCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok();
    }
}
