using Coaching.Application.Queries.GetCoachingAdminOverview;
using Coaching.Application.Queries.GetCoachingAdminAssignments;
using Coaching.Application.Queries.GetAssignment;
using Coaching.Application.Queries.GetCoachingAdminSession;
using Coaching.Application.Queries.GetCoachingAdminExam;
using Coaching.Application.Queries.GetCoachingAdminSessions;
using Coaching.Application.Queries.GetCoachingAdminExams;
using Coaching.Application.Queries.GetCoachingAdminGoals;
using Coaching.Application.Queries;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.CancelAssignment;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Application.Commands.GradeAssignment;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateSessionAttendance;
using Coaching.Application.Commands.DeleteSession;
using Coaching.Application.Commands.CreateExam;
using Coaching.Application.Commands.AddExamResult;
using Coaching.Application.Commands.DeleteExam;
using Coaching.Application.Commands.CreateGoal;
using Coaching.Application.Commands.UpdateGoalProgress;
using Coaching.Application.Commands.DeleteGoal;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin")]
[Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Coaching.View)]
[Produces("application/json")]
public sealed class CoachingAdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoachingAdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns a bounded, read-only operational summary for the global administrator.
    /// Tenant-scoped coaching data remains behind the existing teacher/student policies.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] int recentLimit = 10,
        CancellationToken cancellationToken = default)
    {
        var overview = await _mediator.Send(
            new GetCoachingAdminOverviewQuery(recentLimit),
            cancellationToken);
        return Ok(overview);
    }

    [HttpGet("assignments/{id:guid}")]
    [ProducesResponseType(typeof(AssignmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignment(
        Guid id,
        CancellationToken cancellationToken)
    {
        var assignment = await _mediator.Send(
            new GetAssignmentQuery(id),
            cancellationToken);

        return assignment is null ? NotFound() : Ok(assignment);
    }

    [HttpGet("sessions/{id:guid}")]
    [ProducesResponseType(typeof(CoachingAdminSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(
        Guid id,
        CancellationToken cancellationToken)
    {
        var session = await _mediator.Send(
            new GetCoachingAdminSessionQuery(id),
            cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("exams/{id:guid}")]
    [ProducesResponseType(typeof(CoachingAdminExamDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetExam(
        Guid id,
        CancellationToken cancellationToken)
    {
        var exam = await _mediator.Send(
            new GetCoachingAdminExamQuery(id),
            cancellationToken);
        return exam is null ? NotFound() : Ok(exam);
    }

    [HttpGet("assignments")]
    [ProducesResponseType(typeof(PagedResponse<CoachingAdminAssignmentListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAssignments(
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize,
        [FromQuery] string? status = null,
        [FromQuery] string? source = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var assignments = await _mediator.Send(
            new GetCoachingAdminAssignmentsQuery(pageNumber, pageSize, status, source, search),
            cancellationToken);
        return Ok(assignments);
    }

    /// <summary>
    /// Creates an assignment on behalf of a system administrator.
    /// The command handler remains the single source of truth for tenant and target validation.
    /// </summary>
    [HttpPost("assignments")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateAssignmentResponse>> CreateAssignment(
        [FromBody] CreateAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with { IdempotencyKey = idempotencyKey },
                cancellationToken);

            return CreatedAtAction(nameof(GetAssignment), new { id = result.AssignmentId }, result);
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex) when (ex.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    /// <summary>Soft-cancels an assignment on behalf of a system administrator.</summary>
    [HttpPost("assignments/{id:guid}/cancel")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> CancelAssignment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new CancelAssignmentCommand(id), cancellationToken);
            return Ok(new { message = "Assignment cancelled successfully" });
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

    /// <summary>Hard-deletes an assignment on behalf of a system administrator.</summary>
    [HttpDelete("assignments/{id:guid}")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteAssignmentCommand(id), cancellationToken);
            return NoContent();
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

    /// <summary>Grades an assigned student's work on behalf of a system administrator.</summary>
    [HttpPost("assignments/{id:guid}/grade")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<GradeAssignmentResponse>> GradeAssignment(
        Guid id,
        [FromBody] GradeAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.AssignmentId)
        {
            return BadRequest(new { error = "Assignment ID mismatch" });
        }

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
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpPost("sessions")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with { IdempotencyKey = idempotencyKey },
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex) when (ex.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpPost("sessions/{id:guid}/attendance")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> UpdateSessionAttendance(
        Guid id,
        [FromBody] UpdateSessionAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.SessionId)
        {
            return BadRequest(new { error = "Session ID mismatch" });
        }

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
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpPost("sessions/{id:guid}/cancel")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken cancellationToken)
    {
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
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
    }

    [HttpDelete("sessions/{id:guid}")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken)
    {
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
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
    }

    [HttpPost("exams")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateExamResponse>> CreateExam(
        [FromBody] CreateExamCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with { IdempotencyKey = idempotencyKey },
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex) when (ex.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpPost("exams/{id:guid}/results")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> AddExamResult(
        Guid id,
        [FromBody] AddExamResultCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.ExamId)
        {
            return BadRequest(new { error = "Exam ID mismatch" });
        }

        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            await _mediator.Send(command with { IdempotencyKey = idempotencyKey }, cancellationToken);
            return Ok(new { message = "Result added successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex) when (ex.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpDelete("exams/{id:guid}")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteExam(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteExamCommand(id), cancellationToken);
            return NoContent();
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

    [HttpPost("goals")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateGoalResponse>> CreateGoal(
        [FromBody] CreateGoalCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with { IdempotencyKey = idempotencyKey },
                cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (BusinessRuleException ex) when (ex.Code.Equals("Idempotency.Conflict", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpPut("goals/{id:guid}/progress")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> UpdateGoalProgress(
        Guid id,
        [FromBody] UpdateGoalProgressCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.GoalId)
        {
            return BadRequest(new { error = "Goal ID mismatch" });
        }

        try
        {
            await _mediator.Send(command, cancellationToken);
            return Ok(new { message = "Goal progress updated successfully" });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message, code = ex.Code });
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
    }

    [HttpDelete("goals/{id:guid}")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            await _mediator.Send(new DeleteGoalCommand(id), cancellationToken);
            return NoContent();
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

    [HttpGet("sessions")]
    [ProducesResponseType(typeof(PagedResponse<CoachingAdminSessionListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var sessions = await _mediator.Send(
            new GetCoachingAdminSessionsQuery(pageNumber, pageSize, status, search),
            cancellationToken);
        return Ok(sessions);
    }

    [HttpGet("exams")]
    [ProducesResponseType(typeof(PagedResponse<CoachingAdminExamListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExams(
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize,
        [FromQuery] string? examType = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var exams = await _mediator.Send(
            new GetCoachingAdminExamsQuery(pageNumber, pageSize, examType, search),
            cancellationToken);
        return Ok(exams);
    }

    [HttpGet("goals")]
    [ProducesResponseType(typeof(PagedResponse<CoachingAdminGoalListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGoals(
        [FromQuery] int pageNumber = CoachingPaging.DefaultPageNumber,
        [FromQuery] int pageSize = CoachingPaging.DefaultPageSize,
        [FromQuery] bool? completed = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var goals = await _mediator.Send(
            new GetCoachingAdminGoalsQuery(pageNumber, pageSize, completed, search),
            cancellationToken);
        return Ok(goals);
    }
}
