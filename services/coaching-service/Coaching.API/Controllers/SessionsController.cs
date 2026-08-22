using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateSessionAttendance;
using Coaching.Application.Commands.UpdateSessionStudentNote;
using Coaching.Application.Commands.UpdateSession;
using Coaching.Application.Queries.GetSessions;
using Coaching.Application.Queries;
using MediatR;
using Coaching.Application.Commands.DeleteSession;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.API.Controllers;

/// <summary>
/// Coaching Sessions Management API
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/[controller]")]
[Produces("application/json")]
public class SessionsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<SessionsController> _logger;

    public SessionsController(
        IMediator mediator,
        ILogger<SessionsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Schedule a new coaching session
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduling session for student: {StudentId} with teacher: {TeacherId}", 
            command.StudentId, command.TeacherId);

        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with { IdempotencyKey = idempotencyKey },
                cancellationToken);
            return CreatedAtAction(nameof(GetTeacherSessions), new { teacherId = command.TeacherId }, result);
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Idempotency.", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session");
            return StatusCode(500, new { error = "An error occurred while creating the session" });
        }
    }

    /// <summary>
    /// Update session attendance
    /// </summary>
    [HttpPost("{id}/attendance")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAttendance(
        Guid id,
        [FromBody] UpdateSessionAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.SessionId)
            return BadRequest("Session ID mismatch");

        _logger.LogInformation("Updating attendance for session: {SessionId}", id);

        try
        {
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Attendance updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating attendance");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Cancel a session
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling session: {SessionId}", id);

        try
        {
            await _mediator.Send(new CancelSessionCommand(id), cancellationToken);
            return Ok(new { message = "Session cancelled successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.Code });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling session: {SessionId}", id);
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a session (Hard Delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting session: {SessionId}", id);

        try
        {
            await _mediator.Send(new DeleteSessionCommand(id), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting session: {SessionId}", id);
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get sessions for a teacher
    /// </summary>
    [HttpGet("teacher/{teacherId}")]
    [ProducesResponseType(typeof(PagedResponse<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherSessions(
        Guid teacherId,
        CancellationToken cancellationToken,
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize)
    {
        var result = await _mediator.Send(
            new GetTeacherSessionsQuery(teacherId, pageNumber, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Gets one session for its owning teacher, including only identity-authorized students.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _mediator.Send(new GetSessionQuery(id), cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
    }

    /// <summary>
    /// Reschedule a session and replace its editable details.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UpdateSessionResponse>> UpdateSession(
        Guid id,
        [FromBody] UpdateSessionCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.SessionId)
            return BadRequest(new { error = "Session ID mismatch" });

        try
        {
            return Ok(await _mediator.Send(command, cancellationToken));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Save a student's private reflection for an attended session.
    /// </summary>
    [HttpPut("{id}/student-note")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStudentNote(
        Guid id,
        [FromBody] UpdateSessionStudentNoteCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.SessionId)
            return BadRequest("Session ID mismatch");

        try
        {
            await _mediator.Send(command, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating student note for session: {SessionId}", id);
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get sessions for a student or an authorized parent viewer.
    /// </summary>
    [HttpGet("student/{studentId}")]
    [ProducesResponseType(typeof(PagedResponse<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentSessions(
        Guid studentId,
        CancellationToken cancellationToken,
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize)
    {
        var result = await _mediator.Send(
            new GetStudentSessionsQuery(studentId, pageNumber, pageSize),
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get upcoming sessions
    /// </summary>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(PagedResponse<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingSessions(
        CancellationToken cancellationToken,
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize)
    {
        var result = await _mediator.Send(
            new GetUpcomingSessionsQuery(DateTime.UtcNow, pageNumber, pageSize),
            cancellationToken);

        return Ok(result);
    }
}
