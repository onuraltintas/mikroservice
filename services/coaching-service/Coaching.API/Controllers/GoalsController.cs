using Microsoft.AspNetCore.Mvc;
using Coaching.Application.Commands.CreateGoal;
using Coaching.Application.Commands.UpdateGoalProgress;
using Coaching.Application.Queries.GetGoals;

using MediatR;
using Coaching.Application.Commands.DeleteGoal;

namespace Coaching.API.Controllers;

/// <summary>
/// Academic Goals Management API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GoalsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GoalsController> _logger;

    public GoalsController(
        IMediator mediator,
        ILogger<GoalsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create a new academic goal
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateGoalResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateGoalResponse>> CreateGoal(
        [FromBody] CreateGoalCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating goal: {Title} for student: {StudentId}", command.Title, command.StudentId);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetStudentGoals), new { studentId = command.StudentId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating goal");
            return StatusCode(500, new { error = "An error occurred while creating the goal" });
        }
    }

    /// <summary>
    /// Update goal progress
    /// </summary>
    [HttpPut("{id}/progress")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProgress(
        Guid id,
        [FromBody] UpdateGoalProgressCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.GoalId)
            return BadRequest("Goal ID mismatch");

        _logger.LogInformation("Updating progress for goal: {GoalId} to {Progress}%", id, command.Progress);

        try
        {
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Progress updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating progress");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete a goal
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting goal: {GoalId}", id);

        try
        {
            await _mediator.Send(new DeleteGoalCommand(id), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting goal: {GoalId}", id);
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get goals for a student
    /// </summary>
    [HttpGet("student/{studentId}")]
    [ProducesResponseType(typeof(List<GoalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentGoals(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetStudentGoalsQuery(studentId),
            cancellationToken);

        return Ok(result);
    }
}
