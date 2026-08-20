using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Commands.ManageInstitutions;
using Identity.Application.DTOs.Institutions;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetInstitutions;
using Identity.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/institutions")]
[HasPermission(Permissions.Institutions.View)]
public sealed class InstitutionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IInstitutionRepository _institutionRepository;

    public InstitutionsController(IMediator mediator, IInstitutionRepository institutionRepository)
    {
        _mediator = mediator;
        _institutionRepository = institutionRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(
            new GetInstitutionsQuery(pageNumber, pageSize, search, isActive),
            cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { Error = result.Error });
    }

    [HttpPost]
    [HasPermission(Permissions.Institutions.Manage)]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    public async Task<IActionResult> Create(
        [FromBody] CreateInstitutionCommand command,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
        var result = await _mediator.Send(command with { IdempotencyKey = idempotencyKey }, cancellationToken);
        if (result.IsSuccess)
        {
            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Value },
                new { institutionId = result.Value });
        }

        if (result.Error.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { Error = result.Error });
        }

        return BadRequest(new { Error = result.Error });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetInstitutionByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : NotFound();
    }

    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Institutions.Manage)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateInstitutionRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateInstitutionCommand(
            id,
            request.Name,
            request.Address,
            request.City,
            request.District,
            request.Phone,
            request.Email,
            request.Website,
            request.LicenseType,
            request.MaxStudents,
            request.MaxTeachers,
            request.SubscriptionEndDate);

        var result = await _mediator.Send(command, cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    [HttpPost("{id:guid}/active")]
    [HasPermission(Permissions.Institutions.Manage)]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    public async Task<IActionResult> SetActive(
        Guid id,
        [FromBody] SetInstitutionActiveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetInstitutionActiveCommand(id, request.IsActive), cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    [HttpPost("{id:guid}/admins")]
    [HasPermission(Permissions.Institutions.Manage)]
    public async Task<IActionResult> AssignAdmin(
        Guid id,
        [FromBody] AssignInstitutionAdminRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new AssignInstitutionAdminCommand(id, request.UserId, request.Role),
            cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    [HttpGet("{id:guid}/admins")]
    public async Task<IActionResult> GetAdmins(Guid id, CancellationToken cancellationToken)
    {
        var institution = await _mediator.Send(new GetInstitutionByIdQuery(id), cancellationToken);
        if (institution.IsFailure)
        {
            return NotFound();
        }

        return Ok(await _institutionRepository.GetAdminsAsync(id, cancellationToken));
    }

    [HttpPost("{id:guid}/admins/{userId:guid}/active")]
    [HasPermission(Permissions.Institutions.Manage)]
    public async Task<IActionResult> SetAdminActive(
        Guid id,
        Guid userId,
        [FromBody] SetInstitutionActiveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SetInstitutionAdminActiveCommand(id, userId, request.IsActive),
            cancellationToken);
        return result.IsSuccess ? NoContent() : BadRequest(new { Error = result.Error });
    }

    public sealed record SetInstitutionActiveRequest(bool IsActive);

    public sealed record UpdateInstitutionRequest(
        string? Name,
        string? Address,
        string? City,
        string? District,
        string? Phone,
        string? Email,
        string? Website,
        Identity.Domain.Enums.LicenseType? LicenseType,
        int? MaxStudents,
        int? MaxTeachers,
        DateTime? SubscriptionEndDate);
}
