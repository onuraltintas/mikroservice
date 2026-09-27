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
using Coaching.Application.Commands.DeleteExamResult;
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
using EduPlatform.Shared.Security.Interfaces;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Produces("application/json")]
public sealed class CoachingAdminController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICoachingAdminScopeAuthorization _adminScopeAuthorization;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CoachingAdminController(
        IMediator mediator,
        ICoachingAdminScopeAuthorization adminScopeAuthorization,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _mediator = mediator;
        _adminScopeAuthorization = adminScopeAuthorization;
        _identityAuthorizationClient = identityAuthorizationClient;
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

    [HttpGet("scope")]
    [ProducesResponseType(typeof(CoachingAdminReadScopeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CoachingAdminReadScopeDto>> GetScope(CancellationToken cancellationToken)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        return Ok(new CoachingAdminReadScopeDto(scope.IsGlobal, scope.InstitutionId));
    }

    [HttpGet("teachers/{teacherId:guid}/overview")]
    [ProducesResponseType(typeof(TeacherCoachingOverviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherCoachingOverviewDto>> GetTeacherOverview(
        Guid teacherId,
        [FromServices] ICoachingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        return Ok(await repository.GetTeacherOverviewAsync(
            teacherId,
            scope.InstitutionId,
            cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/detail")]
    [ProducesResponseType(typeof(CoachingAdminStudentDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CoachingAdminStudentDetailDto>> GetStudentDetail(
        Guid studentId,
        [FromServices] ICoachingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        if (!scope.IsGlobal && scope.StudentIds?.Contains(studentId) != true)
        {
            return NotFound();
        }

        return Ok(await repository.GetStudentDetailAsync(studentId, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/history")]
    [ProducesResponseType(typeof(PagedRepositoryResult<CoachingAdminStudentHistoryItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStudentHistory(
        Guid studentId,
        [FromServices] ICoachingAdminRepository repository,
        [FromQuery] CoachingStudentHistoryType type = CoachingStudentHistoryType.Assignments,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        if (!scope.IsGlobal && scope.StudentIds?.Contains(studentId) != true)
        {
            return NotFound();
        }

        if (studentId == Guid.Empty || !Enum.IsDefined(type)
            || pageNumber is < 1 or > 1000 || pageSize is < 1 or > 100)
        {
            return BadRequest();
        }

        return Ok(await repository.GetStudentHistoryAsync(
            studentId, type, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("institutions/{institutionId:guid}/students")]
    [ProducesResponseType(typeof(CoachingStudentReportPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStudentRoster(
        Guid institutionId,
        [FromServices] ICoachingIdentityReportClient reportClient,
        [FromServices] ICurrentUserService currentUser,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] Guid? teacherUserId = null,
        [FromQuery] int? gradeLevel = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        if (!scope.IsGlobal && scope.InstitutionId != institutionId) return NotFound();
        if (pageNumber is < 1 or > 1000 || pageSize is < 1 or > 100
            || search?.Length > 100 || teacherUserId == Guid.Empty || gradeLevel is < 1 or > 12)
            return BadRequest();
        return Ok(await reportClient.GetActiveStudentPageAsync(
            currentUser.UserId!.Value, institutionId, gradeLevel, pageNumber, pageSize,
            cancellationToken, search, teacherUserId));
    }

    [HttpGet("institutions/{institutionId:guid}/teachers")]
    [ProducesResponseType(typeof(CoachingTeacherReportPage), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTeacherRoster(
        Guid institutionId,
        [FromServices] ICoachingIdentityReportClient reportClient,
        [FromServices] ICurrentUserService currentUser,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        if (!scope.IsGlobal && scope.InstitutionId != institutionId) return NotFound();
        if (pageNumber is < 1 or > 1000 || pageSize is < 1 or > 100 || search?.Length > 100)
            return BadRequest();
        return Ok(await reportClient.GetActiveTeacherPageAsync(
            currentUser.UserId!.Value, institutionId, pageNumber, pageSize,
            search, cancellationToken));
    }

    [HttpGet("teachers/{teacherId:guid}/analytics")]
    [ProducesResponseType(typeof(TeacherCoachingAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherCoachingAnalyticsDto>> GetTeacherAnalytics(
        Guid teacherId,
        [FromServices] ICoachingAdminRepository repository,
        CancellationToken cancellationToken)
    {
        var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var toDate = DateTime.UtcNow;
        return Ok(await repository.GetTeacherAnalyticsAsync(
            teacherId, scope.InstitutionId, toDate.AddDays(-30), toDate, cancellationToken));
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateAssignmentResponse>> CreateAssignment(
        [FromBody] CreateAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (!await IsTeacherTargetScopeAuthorizedAsync(scope, command.TeacherId, command.StudentIds, cancellationToken))
            {
                return Forbid();
            }

            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with
                {
                    InstitutionId = scope.IsGlobal ? command.InstitutionId : scope.InstitutionId,
                    IsInstitutionAdminOperation = !scope.IsGlobal,
                    IdempotencyKey = idempotencyKey
                },
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> CancelAssignment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (!await AssignmentExistsInScopeAsync(id, scope, cancellationToken)) return NotFound();
            await _mediator.Send(new CancelAssignmentCommand(id)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteAssignment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (!await AssignmentExistsInScopeAsync(id, scope, cancellationToken)) return NotFound();
            await _mediator.Send(new DeleteAssignmentCommand(id)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var assignment = await GetAssignmentInScopeAsync(id, scope, cancellationToken);
            if (assignment is null) return NotFound();
            if (!scope.IsGlobal
                && !await IsTeacherTargetScopeAuthorizedAsync(scope, assignment.TeacherId, [command.StudentId], cancellationToken))
            {
                return Forbid();
            }

            return Ok(await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken));
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var assignment = await GetAssignmentInScopeAsync(id, scope, cancellationToken);
            if (assignment is null) return NotFound();
            if (!scope.IsGlobal && command.StudentIds is not null
                && !await IsTeacherTargetScopeAuthorizedAsync(scope, assignment.TeacherId, command.StudentIds, cancellationToken))
            {
                return Forbid();
            }

            return Ok(await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken));
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateSessionResponse>> CreateSession(
        [FromBody] CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var studentIds = command.Type == Coaching.Domain.Enums.SessionType.Group
                ? command.StudentIds?.Where(studentId => studentId != Guid.Empty).Distinct().ToArray() ?? []
                : [command.StudentId];
            if (!await IsTeacherTargetScopeAuthorizedAsync(scope, command.TeacherId, studentIds, cancellationToken))
            {
                return Forbid();
            }

            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with
                {
                    InstitutionId = scope.IsGlobal ? command.InstitutionId : scope.InstitutionId,
                    IsInstitutionAdminOperation = !scope.IsGlobal,
                    IdempotencyKey = idempotencyKey
                },
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var session = await GetSessionInScopeAsync(id, scope, cancellationToken);
            if (session is null) return NotFound();
            if (!scope.IsGlobal
                && (!command.StudentId.HasValue
                    || !session.Attendances.Any(attendance => attendance.StudentId == command.StudentId.Value)))
            {
                return Forbid();
            }

            await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetSessionInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            return Ok(await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken));
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> CancelSession(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetSessionInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            await _mediator.Send(new CancelSessionCommand(id)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteSession(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetSessionInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            await _mediator.Send(new DeleteSessionCommand(id)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateExamResponse>> CreateExam(
        [FromBody] CreateExamCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (!await IsTeacherTargetScopeAuthorizedAsync(scope, command.TeacherId, [], cancellationToken))
            {
                return Forbid();
            }

            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with
                {
                    InstitutionId = scope.IsGlobal ? command.InstitutionId : scope.InstitutionId,
                    IsInstitutionAdminOperation = !scope.IsGlobal,
                    IdempotencyKey = idempotencyKey
                },
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var exam = await GetExamInScopeAsync(id, scope, cancellationToken);
            if (exam is null) return NotFound();
            if (!scope.IsGlobal
                && !await IsTeacherTargetScopeAuthorizedAsync(scope, exam.CreatedByTeacherId, [command.StudentId], cancellationToken))
            {
                return Forbid();
            }

            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal,
                IdempotencyKey = idempotencyKey
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetExamInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            return Ok(await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken));
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var exam = await GetExamInScopeAsync(id, scope, cancellationToken);
            if (exam is null) return NotFound();
            if (!scope.IsGlobal && !exam.Results.Any(result => result.Id == resultId)) return NotFound();
            return Ok(await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken));
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

    [HttpDelete("exams/{id:guid}/results/{resultId:guid}")]
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteExamResult(
        Guid id,
        Guid resultId,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            var exam = await GetExamInScopeAsync(id, scope, cancellationToken);
            if (exam is null) return NotFound();
            if (!scope.IsGlobal && !exam.Results.Any(result => result.Id == resultId)) return NotFound();
            await _mediator.Send(new DeleteExamResultCommand(id, resultId)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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

    [HttpDelete("exams/{id:guid}")]
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteExam(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetExamInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            await _mediator.Send(new DeleteExamCommand(id)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<ActionResult<CreateGoalResponse>> CreateGoal(
        [FromBody] CreateGoalCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (!scope.IsGlobal
                && (command.TeacherId.HasValue
                    ? !await IsTeacherTargetScopeAuthorizedAsync(scope, command.TeacherId.Value, [command.StudentId], cancellationToken)
                    : scope.StudentIds?.Contains(command.StudentId) != true))
            {
                return Forbid();
            }

            var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
            var result = await _mediator.Send(
                command with
                {
                    IsInstitutionAdminOperation = !scope.IsGlobal,
                    InstitutionId = scope.IsGlobal ? null : scope.InstitutionId,
                    IdempotencyKey = idempotencyKey
                },
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetGoalInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
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
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetGoalInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            return Ok(await _mediator.Send(command with
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken));
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
    [Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Coaching.Manage)]
    public async Task<IActionResult> DeleteGoal(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var scope = await _adminScopeAuthorization.RequireReadScopeAsync(cancellationToken);
            if (await GetGoalInScopeAsync(id, scope, cancellationToken) is null) return NotFound();
            await _mediator.Send(new DeleteGoalCommand(id)
            {
                IsInstitutionAdminOperation = !scope.IsGlobal
            }, cancellationToken);
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

    private async Task<AssignmentResponse?> GetAssignmentInScopeAsync(
        Guid assignmentId,
        CoachingAdminScope scope,
        CancellationToken cancellationToken) =>
        await _mediator.Send(
            new GetAssignmentQuery(
                assignmentId,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
            cancellationToken);

    private async Task<CoachingAdminSessionDetailDto?> GetSessionInScopeAsync(
        Guid sessionId,
        CoachingAdminScope scope,
        CancellationToken cancellationToken) =>
        await _mediator.Send(
            new GetCoachingAdminSessionQuery(
                sessionId,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
            cancellationToken);

    private async Task<CoachingAdminExamDetailDto?> GetExamInScopeAsync(
        Guid examId,
        CoachingAdminScope scope,
        CancellationToken cancellationToken) =>
        await _mediator.Send(
            new GetCoachingAdminExamQuery(
                examId,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
            cancellationToken);

    private async Task<CoachingAdminGoalDetailDto?> GetGoalInScopeAsync(
        Guid goalId,
        CoachingAdminScope scope,
        CancellationToken cancellationToken) =>
        await _mediator.Send(
            new GetCoachingAdminGoalQuery(
                goalId,
                scope.InstitutionId,
                AdministrativeScope: true,
                ScopedStudentIds: scope.StudentIds),
            cancellationToken);

    private async Task<bool> AssignmentExistsInScopeAsync(
        Guid assignmentId,
        CoachingAdminScope scope,
        CancellationToken cancellationToken) =>
        await GetAssignmentInScopeAsync(assignmentId, scope, cancellationToken) is not null;

    private async Task<bool> IsTeacherTargetScopeAuthorizedAsync(
        CoachingAdminScope scope,
        Guid teacherId,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        if (scope.IsGlobal)
        {
            return true;
        }

        if (scope.InstitutionId is not { } institutionId)
        {
            return false;
        }

        var authorizedInstitutionId = await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            teacherId,
            studentIds,
            institutionId,
            isSystemAdministrator: false,
            cancellationToken);

        return authorizedInstitutionId == institutionId;
    }
}

public sealed record CoachingAdminReadScopeDto(bool IsGlobal, Guid? InstitutionId);
