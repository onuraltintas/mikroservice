using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Commands.SubmitSupportRequest;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;

namespace Notification.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
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
        command = command with
        {
            IdempotencyKey = Request.Headers["Idempotency-Key"].ToString()
        };
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(result.Value);
    }

    [HttpPost("reply")]
    [HasPermission(PlatformPermissions.Support.Reply)]
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
