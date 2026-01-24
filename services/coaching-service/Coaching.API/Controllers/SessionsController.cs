using Microsoft.AspNetCore.Mvc;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateSessionAttendance;
using Coaching.Application.Queries.GetSessions;
using MediatR;
using Coaching.Application.Commands.DeleteSession;

namespace Coaching.API.Controllers;

/// <summary>
/// Coaching Sessions Management API
/// </summary>
[ApiController]
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
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Scheduling session for student: {StudentId} with teacher: {TeacherId}", 
            command.StudentId, command.TeacherId);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetTeacherSessions), new { teacherId = command.TeacherId }, result);
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
    [ProducesResponseType(typeof(List<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherSessions(Guid teacherId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetTeacherSessionsQuery(teacherId), 
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get upcoming sessions
    /// </summary>
    [HttpGet("upcoming")]
    [ProducesResponseType(typeof(List<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUpcomingSessions(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetUpcomingSessionsQuery(DateTime.UtcNow), 
            cancellationToken);

        return Ok(result);
    }
}
