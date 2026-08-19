using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Notification.Application.Commands.ManageNotifications;
using Notification.Application.Queries;

namespace Notification.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/email-templates")]
[HasPermission(PlatformPermissions.Notifications.Templates)]
public sealed class EmailTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmailTemplatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetEmailTemplatesQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { Error = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateEmailTemplateCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess
            ? Created($"/api/email-templates/{result.Value}", new { templateId = result.Value })
            : BadRequest(new { Error = result.Error });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEmailTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateEmailTemplateCommand(id, request.Category, request.Subject, request.Body, request.IsActive),
            cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    public sealed record UpdateEmailTemplateRequest(
        string Category,
        string Subject,
        string Body,
        bool IsActive);
}
