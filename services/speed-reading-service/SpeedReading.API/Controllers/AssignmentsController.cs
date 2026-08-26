using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/assignments")]
[Authorize]
public sealed class AssignmentsController(ISpeedReadingAssignments assignments) : ControllerBase
{
    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAssignmentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var teacherId))
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var id = await assignments.CreateAsync(teacherId, request, cancellationToken);
        return id.HasValue ? Ok(id.Value) : BadRequest("Assignment data is invalid.");
    }

    [HttpGet("my-assignments")]
    public async Task<ActionResult<IReadOnlyList<StudentAssignmentSummary>>> GetMyAssignments(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var studentId))
        {
            return Unauthorized();
        }

        return Ok(await assignments.GetMyAssignmentsAsync(studentId, cancellationToken));
    }

    [HttpGet("teacher-assignments")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<SpeedReadingPage<AssignmentSummary>>> GetTeacherAssignments(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? exerciseTypeId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var teacherId))
        {
            return Unauthorized();
        }

        return Ok(await assignments.GetTeacherAssignmentsAsync(
            teacherId,
            pageNumber,
            pageSize,
            searchTerm,
            isActive,
            exerciseTypeId,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var teacherId))
        {
            return Unauthorized();
        }

        return await assignments.DeleteAsync(teacherId, id, cancellationToken)
            ? NoContent()
            : NotFound();
    }

    [HttpGet("{id:guid}/details")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<AssignmentDetails>> GetDetails(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var teacherId))
        {
            return Unauthorized();
        }

        var result = await assignments.GetDetailsAsync(teacherId, id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/students")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<IActionResult> AddStudent(
        Guid id,
        [FromBody] AddAssignmentStudentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var teacherId))
        {
            return Unauthorized();
        }

        if (request is null || request.StudentId == Guid.Empty)
        {
            return BadRequest("A valid student is required.");
        }

        var result = await assignments.AddStudentAsync(teacherId, id, request.StudentId, cancellationToken);
        return result switch
        {
            AssignmentStudentMutationStatus.Success => Ok(),
            AssignmentStudentMutationStatus.AlreadyAssigned => BadRequest("Student already assigned."),
            AssignmentStudentMutationStatus.StudentNotFound => BadRequest("Student not found."),
            _ => NotFound()
        };
    }

    [HttpDelete("{id:guid}/students/{studentId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<IActionResult> RemoveStudent(
        Guid id,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var teacherId))
        {
            return Unauthorized();
        }

        var result = await assignments.RemoveStudentAsync(teacherId, id, studentId, cancellationToken);
        return result switch
        {
            AssignmentStudentMutationStatus.Success => NoContent(),
            AssignmentStudentMutationStatus.AssignmentNotFound => NotFound(),
            _ => NotFound()
        };
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}

public sealed record AddAssignmentStudentRequest(Guid StudentId);
