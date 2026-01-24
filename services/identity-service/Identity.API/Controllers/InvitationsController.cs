using Identity.Application.Commands.AcceptInvitation;
using Identity.Application.Commands.RejectInvitation;
using Identity.Application.Queries.GetMyInvitations;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/invitations")]
[Authorize]
public class InvitationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public InvitationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Kullanıcının bekleyen davetlerini listeler.
    /// </summary>
    [HttpGet("my-invitations")]
    [ProducesResponseType(typeof(List<InvitationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyInvitations()
    {
        var result = await _mediator.Send(new GetMyInvitationsQuery());
        
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Daveti kabul eder ve kullanıcıyı ilgili kuruma/öğretmene bağlar.
    /// </summary>
    [HttpPost("{id}/accept")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptInvitation(Guid id)
    {
        var result = await _mediator.Send(new AcceptInvitationCommand(id));
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Daveti reddeder.
    /// </summary>
    [HttpPost("{id}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectInvitation(Guid id)
    {
        var result = await _mediator.Send(new RejectInvitationCommand(id));
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }
}
