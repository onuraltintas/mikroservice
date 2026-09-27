using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Institutions;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/institutions/{institutionId:guid}/assignments")]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.Institutions.Manage)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class InstitutionAssignmentsController(
    ISpeedReadingAssignments assignments,
    ISpeedReadingInstitutionAdministrationAuthorization authorization,
    ISpeedReadingUserDirectory userDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid institutionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? exerciseTypeId = null,
        [FromQuery] Guid? teacherId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        if (pageNumber < 1 || pageSize is < 1 or > 100) return BadRequest("Invalid page.");
        var page = await assignments.GetInstitutionAssignmentsAsync(
            institutionId, pageNumber, pageSize, searchTerm, isActive,
            exerciseTypeId, teacherId, cancellationToken);
        var teacherIds = page.Items.Select(item => item.TeacherId).Distinct().ToArray();
        var users = teacherIds.Length == 0
            ? new Dictionary<Guid, SpeedReadingUserDirectoryItem>()
            : (await userDirectory.GetUsersAsync(teacherIds, cancellationToken))
                .Users.ToDictionary(item => item.UserId);
        return Ok(new
        {
            items = page.Items.Select(item => new
            {
                item.Id, item.TeacherId,
                teacherName = users.TryGetValue(item.TeacherId, out var teacher)
                    ? $"{teacher.FirstName} {teacher.LastName}".Trim() : null,
                item.ExerciseId, item.ReadingTextId, item.Title, item.Description,
                item.ExerciseTitle, item.ExerciseTypeName, item.ReadingTextTitle,
                item.DueDate, item.IsActive, item.CreatedAt, item.StudentCount, item.CompletedCount
            }),
            page.TotalCount, page.PageNumber, page.PageSize
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid institutionId, [FromBody] CreateInstitutionAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (!await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        if (request.TeacherId == Guid.Empty) return BadRequest("Teacher is required.");
        var id = await assignments.CreateForInstitutionAsync(
            institutionId, request.TeacherId, actorId,
            new CreateAssignmentRequest(request.ExerciseId, request.ReadingTextId,
                request.StudentIds, request.Title, request.Description, request.DueDate),
            cancellationToken);
        return id.HasValue ? Ok(id.Value) : BadRequest("Teacher, students or assignment data are invalid for this institution.");
    }

    [HttpGet("{id:guid}/details")]
    public async Task<IActionResult> Details(
        Guid institutionId, Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        var details = await assignments.GetInstitutionDetailsAsync(institutionId, id, cancellationToken);
        return details is null ? NotFound() : Ok(details);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid institutionId, Guid id, CancellationToken cancellationToken = default)
    {
        if (!await CanManageAsync(institutionId, cancellationToken)) return Forbid();
        return await assignments.DeleteInstitutionAsync(institutionId, id, cancellationToken)
            ? NoContent() : NotFound();
    }

    private async Task<bool> CanManageAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        if (institutionId == Guid.Empty || !TryGetActor(out var actorId)) return false;
        return User.IsInRole("SystemAdmin")
            || await authorization.CanManageAsync(actorId, institutionId, cancellationToken);
    }

    private bool TryGetActor(out Guid actorId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}

public sealed record CreateInstitutionAssignmentRequest(
    Guid TeacherId, Guid ExerciseId, Guid? ReadingTextId,
    IReadOnlyList<Guid>? StudentIds, string Title, string? Description, DateTime? DueDate);
