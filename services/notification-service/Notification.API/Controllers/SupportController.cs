using MediatR;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Commands.SubmitSupportRequest;

namespace Notification.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SupportController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupportController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitSupportRequestCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("reply")]
    public async Task<IActionResult> Reply([FromBody] Notification.Application.Commands.ReplyToSupportRequest.ReplyToSupportRequestCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok();
    }
}
