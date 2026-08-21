using Coaching.Application.Queries.GetCoachingAdminOverview;
using Coaching.Application.Queries.GetCoachingAdminAssignments;
using Coaching.Application.Queries.GetAssignment;
using Coaching.Application.Queries.GetCoachingAdminSessions;
using Coaching.Application.Queries.GetCoachingAdminExams;
using Coaching.Application.Queries.GetCoachingAdminGoals;
using Coaching.Application.Queries;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Contracts.Authorization;
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
