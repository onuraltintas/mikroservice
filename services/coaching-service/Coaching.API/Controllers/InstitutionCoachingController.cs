using Coaching.Application.Authorization;
using Coaching.Application.Commands.CancelAssignment;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateSessionAttendance;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Application.Commands.DeleteSession;
using Coaching.Application.Commands.GradeAssignment;
using Coaching.Application.Commands.UpdateAssignment;
using Coaching.Application.Commands.UpdateSession;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/institution-coaching")]
[Authorize(Roles = "InstitutionAdmin,InstitutionOwner")]
[Authorize(Policy = "MfaRequired")]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class InstitutionCoachingController(
    IMediator mediator,
    CoachingInstitutionWriteAuthorization authorization) : ControllerBase
{
    [HttpPost("assignments")]
    public async Task<IActionResult> CreateAssignment(
        [FromBody] CreateAssignmentCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await authorization.AuthorizeNewAssignmentAsync(
                command.TeacherId, command.InstitutionId, command.StudentIds ?? [], cancellationToken);
            var result = await mediator.Send(
                command with { IdempotencyKey = Request.Headers["Idempotency-Key"].ToString() },
                cancellationToken);
            return Created($"/api/coaching-admin/assignments/{result.AssignmentId}", result);
        });

    [HttpPost("sessions")]
    public async Task<IActionResult> CreateSession(
        [FromBody] CreateSessionCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            await authorization.AuthorizeNewSessionAsync(
                command.TeacherId, command.StudentId, command.Type, command.StudentIds, cancellationToken);
            var result = await mediator.Send(
                command with { IdempotencyKey = Request.Headers["Idempotency-Key"].ToString() },
                cancellationToken);
            return Created($"/api/coaching-admin/sessions/{result.SessionId}", result);
        });

    [HttpPut("assignments/{id:guid}")]
    public async Task<IActionResult> UpdateAssignment(
        Guid id, [FromBody] UpdateAssignmentCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (id != command.AssignmentId) return BadRequest(new { error = "Assignment ID mismatch" });
            if (!await authorization.AuthorizeAssignmentAsync(id, cancellationToken)) return NotFound();
            return Ok(await mediator.Send(command, cancellationToken));
        });

    [HttpPost("assignments/{id:guid}/cancel")]
    public async Task<IActionResult> CancelAssignment(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (!await authorization.AuthorizeAssignmentAsync(id, cancellationToken)) return NotFound();
            await mediator.Send(new CancelAssignmentCommand(id), cancellationToken);
            return NoContent();
        });

    [HttpPost("assignments/{id:guid}/grade")]
    public async Task<IActionResult> GradeAssignment(
        Guid id, [FromBody] GradeAssignmentCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (id != command.AssignmentId) return BadRequest(new { error = "Assignment ID mismatch" });
            if (command.Score < 0) return BadRequest(new { error = "Score cannot be negative" });
            if (!await authorization.AuthorizeAssignmentAsync(id, cancellationToken)) return NotFound();
            return Ok(await mediator.Send(command, cancellationToken));
        });

    [HttpDelete("assignments/{id:guid}")]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (!await authorization.AuthorizeAssignmentAsync(id, cancellationToken)) return NotFound();
            await mediator.Send(new DeleteAssignmentCommand(id), cancellationToken);
            return NoContent();
        });

    [HttpPut("sessions/{id:guid}")]
    public async Task<IActionResult> UpdateSession(
        Guid id, [FromBody] UpdateSessionCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (id != command.SessionId) return BadRequest(new { error = "Session ID mismatch" });
            if (!await authorization.AuthorizeSessionAsync(id, cancellationToken)) return NotFound();
            return Ok(await mediator.Send(command, cancellationToken));
        });

    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (!await authorization.AuthorizeSessionAsync(id, cancellationToken)) return NotFound();
            await mediator.Send(new DeleteSessionCommand(id), cancellationToken);
            return NoContent();
        });

    [HttpPost("sessions/{id:guid}/cancel")]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (!await authorization.AuthorizeSessionAsync(id, cancellationToken)) return NotFound();
            await mediator.Send(new CancelSessionCommand(id), cancellationToken);
            return NoContent();
        });

    [HttpPost("sessions/{id:guid}/attendance")]
    public async Task<IActionResult> UpdateAttendance(
        Guid id, [FromBody] UpdateSessionAttendanceCommand command, CancellationToken cancellationToken) =>
        await ExecuteAsync(async () =>
        {
            if (id != command.SessionId) return BadRequest(new { error = "Session ID mismatch" });
            if (!await authorization.AuthorizeSessionAsync(id, cancellationToken)) return NotFound();
            await mediator.Send(command, cancellationToken);
            return NoContent();
        });

    private async Task<IActionResult> ExecuteAsync(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        { return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "Idempotency.Conflict")
        { return Conflict(new { error = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex)
        { return BadRequest(new { error = ex.Message, code = ex.Code }); }
        catch (ValidationException ex)
        { return BadRequest(new { error = ex.Message, details = ex.Errors }); }
        catch (InvalidOperationException ex)
        { return NotFound(new { error = ex.Message }); }
        catch (ArgumentException ex)
        { return BadRequest(new { error = ex.Message }); }
    }
}
