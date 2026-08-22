using Coaching.Application.Queries.GetCoachingAdminOverview;
using Coaching.Application.Queries.GetCoachingAdminAssignments;
using Coaching.Application.Queries.GetAssignment;
using Coaching.Application.Queries.GetCoachingAdminSession;
using Coaching.Application.Queries.GetCoachingAdminExam;
using Coaching.Application.Queries.GetCoachingAdminSessions;
using Coaching.Application.Queries.GetCoachingAdminExams;
using Coaching.Application.Queries.GetCoachingAdminGoals;
using Coaching.Application.Queries.GetCoachingAdminGoal;
using Coaching.Application.Queries;
using Coaching.Application.Commands.CreateAssignment;
using Coaching.Application.Commands.CancelAssignment;
using Coaching.Application.Commands.DeleteAssignment;
using Coaching.Application.Commands.GradeAssignment;
using Coaching.Application.Commands.UpdateAssignment;
using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.UpdateSessionAttendance;
using Coaching.Application.Commands.UpdateSession;
using Coaching.Application.Commands.DeleteSession;
using Coaching.Application.Commands.CreateExam;
using Coaching.Application.Commands.AddExamResult;
using Coaching.Application.Commands.UpdateExam;
using Coaching.Application.Commands.DeleteExam;
using Coaching.Application.Commands.CreateGoal;
using Coaching.Application.Commands.UpdateGoalProgress;
using Coaching.Application.Commands.UpdateGoal;
using Coaching.Application.Commands.DeleteGoal;
using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
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
[Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[Produces("application/json")]
public sealed class CoachingAdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICoachingAdminScopeAuthorization _adminScopeAuthorization;

    public CoachingAdminController(
        IMediator mediator,
        ICoachingAdminScopeAuthorization adminScopeAuthorization)
    {
        _mediator = mediator;
        _adminScopeAuthorization = adminScopeAuthorization;
    }

    /// <summary>
    /// Returns a bounded, read-only operational summary for a system administrator or
    /// the authenticated institution administrator's active institution.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] int recentLimit = 10,
        CancellationToken cancellationToken = default)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var overview = await _mediator.Send(
            new GetCoachingAdminOverviewQuery(
                recentLimit,
                scope.InstitutionId,
                scope.StudentIds),
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var assignment = await _mediator.Send(
            new GetAssignmentQuery(
                id,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var session = await _mediator.Send(
            new GetCoachingAdminSessionQuery(
                id,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var exam = await _mediator.Send(
            new GetCoachingAdminExamQuery(
                id,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var assignments = await _mediator.Send(
            new GetCoachingAdminAssignmentsQuery(
                pageNumber,
                pageSize,
                status,
                source,
                search,
                scope.InstitutionId),
            cancellationToken);
        return Ok(assignments);
    }

    /// <summary>
    /// Creates an assignment on behalf of a system administrator.
    /// The command handler remains the single source of truth for tenant and target validation.
    /// </summary>
    [HttpPost("assignments")]
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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

    [HttpPut("assignments/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<UpdateAssignmentResponse>> UpdateAssignment(
        Guid id,
        [FromBody] UpdateAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.AssignmentId)
            return BadRequest(new { error = "Assignment ID mismatch" });

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

    [HttpPost("sessions")]
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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

    [HttpPut("sessions/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
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
        catch (ValidationException ex)
        {
            return BadRequest(new { error = ex.Message, details = ex.Errors });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("sessions/{id:guid}/cancel")]
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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

    [HttpPut("exams/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<UpdateExamResponse>> UpdateExam(
        Guid id,
        [FromBody] UpdateExamCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.ExamId)
            return BadRequest(new { error = "Exam ID mismatch" });

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

    [HttpPut("exams/{id:guid}/results/{resultId:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<UpdateExamResultResponse>> UpdateExamResult(
        Guid id,
        Guid resultId,
        [FromBody] UpdateExamResultCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.ExamId || resultId != command.ResultId)
            return BadRequest(new { error = "Exam or result ID mismatch" });

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

    [HttpDelete("exams/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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
    [Authorize(Roles = "SystemAdmin")]
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

    [HttpPut("goals/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<UpdateGoalResponse>> UpdateGoal(
        Guid id,
        [FromBody] UpdateGoalCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.GoalId)
            return BadRequest(new { error = "Goal ID mismatch" });

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

    [HttpDelete("goals/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var sessions = await _mediator.Send(
            new GetCoachingAdminSessionsQuery(
                pageNumber,
                pageSize,
                status,
                search,
                scope.InstitutionId),
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var exams = await _mediator.Send(
            new GetCoachingAdminExamsQuery(
                pageNumber,
                pageSize,
                examType,
                search,
                scope.InstitutionId),
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
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var goals = await _mediator.Send(
            new GetCoachingAdminGoalsQuery(
                pageNumber,
                pageSize,
                completed,
                search,
                scope.InstitutionId,
                scope.StudentIds),
            cancellationToken);
        return Ok(goals);
    }

    [HttpGet("goals/{id:guid}")]
    [ProducesResponseType(typeof(CoachingAdminGoalDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetGoal(
        Guid id,
        CancellationToken cancellationToken)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var goal = await _mediator.Send(
            new GetCoachingAdminGoalQuery(
                id,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
            cancellationToken);
        return goal is null ? NotFound() : Ok(goal);
    }
}
