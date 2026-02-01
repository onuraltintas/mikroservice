using Identity.Application.Commands.Notification.ForwardSupportRequest;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/internal/notification")]
public class InternalNotificationController : ControllerBase
{
    private readonly IMediator _mediator;

    public InternalNotificationController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("forward-support")]
    [AllowAnonymous] // In a real app, this would be restricted to internal network or have a secret key
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
