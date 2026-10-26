using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Commands.ManageNotifications;
using Notification.Application.Commands.ReplyToSupportRequest;
using Notification.Application.Queries;

namespace Notification.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/support/requests")]
[HasPermission(PlatformPermissions.Support.View)]
public sealed class SupportAdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public SupportAdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool? isProcessed = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetSupportRequestsQuery(pageNumber, pageSize, isProcessed, search),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { Error = result.Error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetSupportRequestQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPost("{id:guid}/process")]
    [HasPermission(PlatformPermissions.Support.Reply)]
    [MfaCategory(MfaOperationCategories.Cms)]
    public async Task<IActionResult> Process(
        Guid id,
        [FromBody] ProcessRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ProcessSupportRequestCommand(id, request.AdminNote), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    [HttpPost("{id:guid}/reply")]
    [HasPermission(PlatformPermissions.Support.Reply)]
    [MfaCategory(MfaOperationCategories.Cms)]
    public async Task<IActionResult> Reply(
        Guid id,
        [FromBody] ReplyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReplyToSupportRequestCommand(id, request.ReplyMessage), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Deletes the request and the sender's data held with it. Support.Delete is a management
    /// permission, so it follows the content MFA policy like replying; the admin audit log
    /// records who deleted which request.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.Support.Delete)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteSupportRequestCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : NotFound();
    }

    public sealed record ProcessRequest(string? AdminNote);
    public sealed record ReplyRequest(string ReplyMessage);
}
