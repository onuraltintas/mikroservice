using Microsoft.AspNetCore.Mvc;
using Coaching.Application.Commands.CreateExam;
using Coaching.Application.Commands.AddExamResult;
using Coaching.Application.Queries.GetExamResults;

using MediatR;
using Coaching.Application.Commands.DeleteExam;

namespace Coaching.API.Controllers;

/// <summary>
/// Exams Management API
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExamsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ExamsController> _logger;

    public ExamsController(
        IMediator mediator,
        ILogger<ExamsController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Create a new exam definition
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CreateExamResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateExamResponse>> CreateExam(
        [FromBody] CreateExamCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating exam: {Title}", command.Title);

        try
        {
            var result = await _mediator.Send(command, cancellationToken);
            return CreatedAtAction(nameof(GetStudentResults), new { studentId = Guid.Empty }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating exam");
            return StatusCode(500, new { error = "An error occurred while creating the exam" });
        }
    }

    /// <summary>
    /// Add custom student result to an exam
    /// </summary>
    [HttpPost("{id}/results")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddExamResult(
        Guid id,
        [FromBody] AddExamResultCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.ExamId)
            return BadRequest("Exam ID mismatch");

        _logger.LogInformation("Adding result to exam: {ExamId} for student: {StudentId}", id, command.StudentId);

        try
        {
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Result added successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding exam result");
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Delete an exam
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteExam(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting exam: {ExamId}", id);

        try
        {
            await _mediator.Send(new DeleteExamCommand(id), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting exam: {ExamId}", id);
            return StatusCode(500, new { error = "An error occurred" });
        }
    }

    /// <summary>
    /// Get all exam results for a student
    /// </summary>
    [HttpGet("student/{studentId}")]
    [ProducesResponseType(typeof(List<ExamResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentResults(Guid studentId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetStudentExamResultsQuery(studentId),
            cancellationToken);

        return Ok(result);
    }
}
