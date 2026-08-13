using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Commands.SubmitSupportRequest;

namespace Notification.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SupportController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupportController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("submit")]
    [RequestSizeLimit(32_768)]
    [RequestTimeout(milliseconds: 10_000)]
    [AllowAnonymous]
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
    [Authorize]
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
