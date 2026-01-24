using Microsoft.AspNetCore.Mvc;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.SubmitAssignment;
using Coaching.Application.Commands.GradeAssignment;
using Coaching.Application.Commands.CancelAssignment;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Application.Queries.GetAssignment;
using Coaching.Application.Queries.GetTeacherAssignments;
using Coaching.Application.Queries.GetStudentAssignments;
using MediatR;

namespace Coaching.API.Controllers;

/// <summary>
/// Assignments Management API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AssignmentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AssignmentsController> _logger;

    public AssignmentsController(
        IMediator mediator,
        ILogger<AssignmentsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create a new assignment
    /// </summary>
    /// <param name="command">Assignment details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created assignment</returns>
    [HttpPost]
    [ProducesResponseType(typeof(CreateAssignmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CreateAssignmentResponse>> CreateAssignment(
        [FromBody] CreateAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating assignment: {Title} for teacher: {TeacherId}", 
            command.Title, command.TeacherId);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Assignment created successfully: {AssignmentId}", result.AssignmentId);

            return CreatedAtAction(
                nameof(GetAssignment),
                new { id = result.AssignmentId },
                result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating assignment: {Title}", command.Title);
            return StatusCode(500, new { error = "An error occurred while creating the assignment" });
        }
    }

    /// <summary>
    /// Submit an assignment
    /// </summary>
    [HttpPost("{id}/submit")]
    [ProducesResponseType(typeof(SubmitAssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubmitAssignmentResponse>> SubmitAssignment(
        Guid id,
        [FromBody] SubmitAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.AssignmentId)
            return BadRequest("Assignment ID mismatch");

        _logger.LogInformation("Submitting assignment: {AssignmentId} for student: {StudentId}", id, command.StudentId);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting assignment: {AssignmentId}", id);
            return StatusCode(500, new { error = "An error occurred while submitting the assignment" });
        }
    }

    /// <summary>
    /// Grade an assignment
    /// </summary>
    [HttpPost("{id}/grade")]
    [ProducesResponseType(typeof(GradeAssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradeAssignmentResponse>> GradeAssignment(
        Guid id,
        [FromBody] GradeAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.AssignmentId)
            return BadRequest("Assignment ID mismatch");

        _logger.LogInformation("Grading assignment: {AssignmentId} for student: {StudentId}", id, command.StudentId);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error grading assignment: {AssignmentId}", id);
            return StatusCode(500, new { error = "An error occurred while grading the assignment" });
        }
    }

    /// <summary>
    /// Cancel an assignment (Soft Delete)
    /// </summary>
    [HttpPost("{id}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelAssignment(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling assignment: {AssignmentId}", id);

        try
        {
            await _mediator.Send(new CancelAssignmentCommand(id), cancellationToken);
            return Ok(new { message = "Assignment cancelled successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling assignment: {AssignmentId}", id);
            return StatusCode(500, new { error = "An error occurred while cancelling the assignment" });
        }
    }

    /// <summary>
    /// Delete an assignment (Hard Delete)
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting assignment: {AssignmentId}", id);

        try
        {
            await _mediator.Send(new DeleteAssignmentCommand(id), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting assignment: {AssignmentId}", id);
            return StatusCode(500, new { error = "An error occurred while deleting the assignment" });
        }
    }

    /// <summary>
    /// Get assignment by ID
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignment(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting assignment: {AssignmentId}", id);

        var result = await _mediator.Send(new GetAssignmentQuery(id), cancellationToken);

        if (result == null)
            return NotFound(new { message = "Assignment not found", id });

        return Ok(result);
    }

    /// <summary>
    /// Get assignments for teacher
    /// </summary>
    [HttpGet("teacher/{teacherId}")]
    [ProducesResponseType(typeof(TeacherAssignmentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherAssignments(Guid teacherId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting assignments for teacher: {TeacherId}", teacherId);

        var result = await _mediator.Send(
            new GetTeacherAssignmentsQuery(teacherId), 
            cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get assignments for student
    /// </summary>
    [HttpGet("student/{studentId}")]
    [ProducesResponseType(typeof(StudentAssignmentListResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentAssignments(Guid studentId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting assignments for student: {StudentId}", studentId);

        var result = await _mediator.Send(
            new GetStudentAssignmentsQuery(studentId),
            cancellationToken);

        return Ok(result);
    }
}
