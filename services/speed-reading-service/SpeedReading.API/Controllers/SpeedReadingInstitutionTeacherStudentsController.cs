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
[Route("api/speed-reading/institutions/{institutionId:guid}/teachers/{teacherUserId:guid}/students")]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.Institutions.Manage)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class SpeedReadingInstitutionTeacherStudentsController(
    ISpeedReadingTeacherStudentAssignments assignments,
    ISpeedReadingInstitutionAdministrationAuthorization administrationAuthorization,
    ISpeedReadingInstitutionDirectory institutionDirectory,
    ISpeedReadingUserDirectory userDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAssignments(
        Guid institutionId,
        Guid teacherUserId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(institutionId, cancellationToken))
            return Forbid();
        if (pageNumber < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { code = "ValidationFailed", message = "Sayfa numarası ve sayfa boyutu geçersiz." });
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });

        var page = await assignments.GetStudentsAsync(
            institutionId,
            teacherUserId,
            pageNumber,
            pageSize,
            cancellationToken);
        var userIds = page.Items.Select(item => item.StudentUserId).Distinct().ToArray();
        var usersById = userIds.Length == 0
            ? new Dictionary<Guid, SpeedReadingUserDirectoryItem>()
            : (await userDirectory.GetUsersAsync(userIds, cancellationToken))
                .Users.ToDictionary(item => item.UserId);
        var items = page.Items.Select(item =>
        {
            usersById.TryGetValue(item.StudentUserId, out var user);
            return new SpeedReadingInstitutionTeacherStudentView(
                item.StudentUserId,
                user?.FirstName ?? string.Empty,
                user?.LastName ?? string.Empty,
                user?.Email,
                item.IsActive && user?.IsActive == true,
                item.CreatedAt,
                item.UpdatedAt);
        }).ToArray();

        return Ok(new { items, page.TotalCount, page.PageNumber, page.PageSize });
    }

    [HttpPut("{studentUserId:guid}")]
    public async Task<IActionResult> AssignStudent(
        Guid institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(institutionId, cancellationToken))
            return Forbid();
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });
        if (!TryGetActor(out var actorId))
            return Unauthorized();

        var result = await assignments.AssignAsync(
            institutionId,
            teacherUserId,
            studentUserId,
            actorId,
            DateTime.UtcNow,
            cancellationToken);
        return result switch
        {
            SpeedReadingTeacherStudentAssignmentResult.Created
                or SpeedReadingTeacherStudentAssignmentResult.AlreadyActive => NoContent(),
            SpeedReadingTeacherStudentAssignmentResult.MembershipRequired => Conflict(new
            {
                code = "TeacherStudentAssignment.MembershipRequired",
                message = "Öğretmen ve öğrenci önce bu Hızlı Okuma kurumunun aktif üyeleri olmalıdır."
            }),
            SpeedReadingTeacherStudentAssignmentResult.ProductRoleRequired => Conflict(new
            {
                code = "TeacherStudentAssignment.ProductRoleRequired",
                message = "Öğretmen ve öğrencinin Hızlı Okuma ürün erişimi ve rolü bulunmalıdır."
            }),
            SpeedReadingTeacherStudentAssignmentResult.TeacherAssignmentConflict => Conflict(new
            {
                code = "TeacherStudentAssignment.ReassignmentConflict",
                message = "Öğrenci başka bir öğretmene eş zamanlı atanmış. Listeyi yenileyip tekrar deneyin."
            }),
            _ => Conflict()
        };
    }

    [HttpDelete("{studentUserId:guid}")]
    public async Task<IActionResult> RemoveStudent(
        Guid institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(institutionId, cancellationToken))
            return Forbid();
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });
        if (!TryGetActor(out var actorId))
            return Unauthorized();

        var removed = await assignments.RemoveAsync(
            institutionId,
            teacherUserId,
            studentUserId,
            actorId,
            DateTime.UtcNow,
            cancellationToken);
        return removed ? NoContent() : NotFound();
    }

    private async Task<bool> CanManageInstitutionAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        if (institutionId == Guid.Empty || !TryGetActor(out var actorId))
            return false;
        if (User.IsInRole("SystemAdmin"))
            return true;
        if (!User.IsInRole("InstitutionAdmin") && !User.IsInRole("InstitutionOwner"))
            return false;

        return await administrationAuthorization.CanManageAsync(actorId, institutionId, cancellationToken);
    }

    private async Task<bool> InstitutionExistsAsync(Guid institutionId, CancellationToken cancellationToken)
    {
        var response = await institutionDirectory.GetInstitutionsAsync(cancellationToken);
        return response.Institutions.Any(item => item.InstitutionId == institutionId && item.IsActive);
    }

    private bool TryGetActor(out Guid actorId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
        out actorId);
}

public sealed record SpeedReadingInstitutionTeacherStudentView(
    Guid StudentUserId,
    string FirstName,
    string LastName,
    string? Email,
    bool IsActive,
    DateTime AssignedAt,
    DateTime? UpdatedAt);
