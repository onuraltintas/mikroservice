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
[Route("api/speed-reading/teachers/me/students")]
[Authorize(Roles = "Teacher")]
[HasPermission(PlatformPermissions.SpeedReading.ReportView)]
public sealed class SpeedReadingTeacherStudentsController(
    ISpeedReadingTeacherStudentAssignments assignments,
    ISpeedReadingInstitutionDirectory institutionDirectory,
    ISpeedReadingUserDirectory userDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMyStudents(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? gradeLevel = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? studentUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherUserId))
            return Unauthorized();
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            return BadRequest(new
            {
                code = "ValidationFailed",
                message = "Sayfa numarası ve sayfa boyutu geçersiz."
            });
        }
        if (searchTerm?.Length > 100 || gradeLevel is < 1 or > 12 || studentUserId == Guid.Empty)
        {
            return BadRequest(new
            {
                code = "ValidationFailed",
                message = "Öğrenci filtreleri geçersiz."
            });
        }

        var institutions = await institutionDirectory.GetInstitutionsAsync(cancellationToken);
        var activeInstitutions = institutions.Institutions
            .Where(item => item.IsActive && item.InstitutionId != Guid.Empty)
            .ToDictionary(item => item.InstitutionId);
        var normalizedSearch = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();
        var mustFilterDirectoryFields = normalizedSearch is not null || isActive.HasValue;
        IReadOnlyList<SpeedReadingTeacherStudentRosterRecord> rosterItems;
        Dictionary<Guid, SpeedReadingUserDirectoryItem> usersById;
        int totalCount;
        if (mustFilterDirectoryFields)
        {
            var firstPage = await GetRosterPageAsync(1, 100);
            var candidates = firstPage.Items.ToList();
            var candidatePageCount = (int)Math.Ceiling(firstPage.TotalCount / (double)firstPage.PageSize);
            for (var candidatePage = 2; candidatePage <= candidatePageCount; candidatePage++)
            {
                var nextPage = await GetRosterPageAsync(candidatePage, firstPage.PageSize);
                candidates.AddRange(nextPage.Items);
            }

            usersById = await GetUsersByIdAsync(candidates.Select(item => item.StudentUserId));
            var matching = candidates.Where(item =>
            {
                usersById.TryGetValue(item.StudentUserId, out var user);
                var effectiveIsActive = user?.IsActive == true && item.IsProfileActive != false;
                var matchesStatus = !isActive.HasValue || effectiveIsActive == isActive.Value;
                if (!matchesStatus)
                    return false;
                if (normalizedSearch is null)
                    return true;

                var fullName = $"{user?.FirstName} {user?.LastName}";
                return fullName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                    || (user?.Email?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false);
            }).ToArray();

            totalCount = matching.Length;
            rosterItems = matching.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        }
        else
        {
            var page = await GetRosterPageAsync(pageNumber, pageSize);
            rosterItems = page.Items;
            totalCount = page.TotalCount;
            usersById = await GetUsersByIdAsync(rosterItems.Select(item => item.StudentUserId));
        }

        var items = rosterItems.Select(item =>
        {
            usersById.TryGetValue(item.StudentUserId, out var user);
            var institution = item.InstitutionId.HasValue
                && activeInstitutions.TryGetValue(item.InstitutionId.Value, out var matchingInstitution)
                ? matchingInstitution
                : null;
            return new SpeedReadingTeacherStudentView(
                item.StudentUserId,
                user?.FirstName ?? string.Empty,
                user?.LastName ?? string.Empty,
                user?.Email ?? string.Empty,
                item.InstitutionId,
                institution?.InstitutionName,
                item.CurrentLevel ?? 0,
                item.GradeLevel,
                item.TargetWpm,
                item.TargetComprehension,
                item.DailyGoalMinutes,
                item.LearningStyle,
                user?.IsActive == true && item.IsProfileActive != false,
                item.AssignedAt,
                teacherUserId);
        }).ToArray();

        return Ok(new
        {
            items,
            totalCount,
            pageNumber,
            pageSize
        });

        Task<SpeedReadingTeacherStudentRosterPage> GetRosterPageAsync(int targetPageNumber, int targetPageSize) =>
            assignments.GetTeacherRosterAsync(
                teacherUserId,
                activeInstitutions.Keys.ToArray(),
                targetPageNumber,
                targetPageSize,
                cancellationToken,
                gradeLevel,
                studentUserId);

        async Task<Dictionary<Guid, SpeedReadingUserDirectoryItem>> GetUsersByIdAsync(IEnumerable<Guid> userIds)
        {
            var ids = userIds.Distinct().ToArray();
            return ids.Length == 0
                ? new Dictionary<Guid, SpeedReadingUserDirectoryItem>()
                : (await userDirectory.GetUsersAsync(ids, cancellationToken))
                    .Users.ToDictionary(item => item.UserId);
        }
    }

    [HttpDelete("{studentUserId:guid}")]
    public async Task<IActionResult> RemoveMyStudent(
        Guid studentUserId,
        [FromQuery] Guid? institutionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherUserId))
            return Unauthorized();
        if (studentUserId == Guid.Empty || institutionId == Guid.Empty)
            return BadRequest(new { code = "ValidationFailed", message = "Öğrenci veya kurum bilgisi geçersiz." });

        var removed = await assignments.RemoveAsync(
            institutionId,
            teacherUserId,
            studentUserId,
            teacherUserId,
            DateTime.UtcNow,
            cancellationToken);
        return removed ? NoContent() : NotFound(new
        {
            code = "TeacherStudentAssignment.NotFound",
            message = "Bu öğrenci için etkin Hızlı Okuma bağlantısı bulunamadı."
        });
    }

    private bool TryGetActor(out Guid actorId) => Guid.TryParse(
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"),
        out actorId);
}

public sealed record SpeedReadingTeacherStudentView(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    Guid? InstitutionId,
    string? InstitutionName,
    int CurrentLevel,
    int? GradeLevel,
    int? TargetWpm,
    decimal? TargetComprehension,
    int? DailyGoalMinutes,
    string? LearningStyle,
    bool IsActive,
    DateTime CreatedAt,
    Guid TeacherId);
