using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/institutions/{institutionId:guid}/members")]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.Institutions.Manage)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class SpeedReadingInstitutionMembersController(
    ISpeedReadingInstitutionMemberships memberships,
    ISpeedReadingInstitutionStudentManagement studentManagement,
    ISpeedReadingInstitutionMemberEligibility memberEligibility,
    ISpeedReadingInstitutionAdministrationAuthorization administrationAuthorization,
    ISpeedReadingInstitutionDirectory institutionDirectory,
    ISpeedReadingUserDirectory userDirectory) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMembers(
        Guid institutionId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] SpeedReadingInstitutionMemberRole? role = null,
        [FromQuery] bool? isActive = null,
        [FromQuery] string? searchTerm = null,
        [FromQuery] int? gradeLevel = null,
        [FromQuery] Guid? teacherUserId = null,
        [FromQuery] Guid? memberUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(institutionId, cancellationToken))
            return Forbid();
        if (pageNumber < 1 || pageSize is < 1 or > 100)
            return BadRequest(new { code = "ValidationFailed", message = "Sayfa numarası ve sayfa boyutu geçersiz." });
        if (role.HasValue && !Enum.IsDefined(role.Value))
            return BadRequest(new { code = "ValidationFailed", message = "Öğrenci veya öğretmen rolü seçilmelidir." });
        if (searchTerm?.Length > 100 || gradeLevel is < 1 or > 12 || teacherUserId == Guid.Empty || memberUserId == Guid.Empty)
            return BadRequest(new { code = "ValidationFailed", message = "Kurum üyesi filtreleri geçersiz." });
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });

        var candidates = new List<SpeedReadingInstitutionMembershipRecord>();
        if (string.IsNullOrWhiteSpace(searchTerm) && !isActive.HasValue)
        {
            var page = await GetMemberPageAsync(pageNumber, pageSize, cancellationToken);
            candidates.AddRange(page.Items);
            var usersById = await GetUsersByIdAsync(candidates.Select(item => item.UserId), cancellationToken);
            return Ok(await ToPage(page.TotalCount, page.PageNumber, page.PageSize, candidates, usersById, cancellationToken));
        }

        var firstPage = await GetMemberPageAsync(1, 100, cancellationToken);
        candidates.AddRange(firstPage.Items);
        for (var candidatePage = 2; candidatePage <= Math.Max(1, (int)Math.Ceiling(firstPage.TotalCount / (double)firstPage.PageSize)); candidatePage++)
        {
            var nextPage = await GetMemberPageAsync(candidatePage, firstPage.PageSize, cancellationToken);
            candidates.AddRange(nextPage.Items);
        }

        var allUsersById = await GetUsersByIdAsync(candidates.Select(item => item.UserId), cancellationToken);
        var term = searchTerm?.Trim();
        var matching = candidates.Where(item =>
        {
            allUsersById.TryGetValue(item.UserId, out var user);
            var effectiveIsActive = item.IsActive && user?.IsActive == true && item.IsProfileActive != false;
            if (isActive.HasValue && effectiveIsActive != isActive.Value)
                return false;
            if (string.IsNullOrWhiteSpace(term))
                return true;
            if (user is null)
                return false;

            var fullName = $"{user.FirstName} {user.LastName}";
            return fullName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (user.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);
        }).ToArray();
        var matchingPage = matching.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        return Ok(await ToPage(matching.Length, pageNumber, pageSize, matchingPage, allUsersById, cancellationToken));

        Task<SpeedReadingInstitutionMembershipPage> GetMemberPageAsync(
            int targetPageNumber,
            int targetPageSize,
            CancellationToken token) => memberships.GetMembersAsync(
                institutionId,
                targetPageNumber,
                targetPageSize,
                token,
                role,
                null,
                gradeLevel,
                teacherUserId,
                memberUserId);

        async Task<Dictionary<Guid, SpeedReadingUserDirectoryItem>> GetUsersByIdAsync(
            IEnumerable<Guid> userIds,
            CancellationToken token)
        {
            var ids = userIds.Distinct().ToArray();
            return ids.Length == 0
                ? new Dictionary<Guid, SpeedReadingUserDirectoryItem>()
                : (await userDirectory.GetUsersAsync(ids, token)).Users.ToDictionary(item => item.UserId);
        }

        async Task<object> ToPage(
            int totalCount,
            int resultPageNumber,
            int resultPageSize,
            IReadOnlyList<SpeedReadingInstitutionMembershipRecord> records,
            Dictionary<Guid, SpeedReadingUserDirectoryItem> usersById,
            CancellationToken token)
        {
            var teacherIds = records
                .Where(item => item.TeacherUserId.HasValue)
                .Select(item => item.TeacherUserId!.Value)
                .Where(id => !usersById.ContainsKey(id))
                .Distinct()
                .ToArray();
            if (teacherIds.Length > 0)
            {
                var teachers = await userDirectory.GetUsersAsync(teacherIds, token);
                foreach (var teacher in teachers.Users)
                    usersById.TryAdd(teacher.UserId, teacher);
            }

            var items = records.Select(item =>
            {
                usersById.TryGetValue(item.UserId, out var user);
                var teacherName = item.TeacherUserId.HasValue && usersById.TryGetValue(item.TeacherUserId.Value, out var teacher)
                    ? $"{teacher.FirstName} {teacher.LastName}".Trim()
                    : null;
                return new SpeedReadingInstitutionMemberView(
                    item.UserId,
                    user?.FirstName ?? string.Empty,
                    user?.LastName ?? string.Empty,
                    user?.Email,
                    item.Role,
                    item.IsActive,
                    item.IsActive && user?.IsActive == true && item.IsProfileActive != false,
                    item.CreatedAt,
                    item.UpdatedAt,
                    item.CurrentLevel,
                    item.GradeLevel,
                    item.TargetWpm,
                    item.TargetComprehension,
                    item.DailyGoalMinutes,
                    item.LearningStyle,
                    item.TeacherUserId,
                    teacherName,
                    item.StudentCount);
            }).ToArray();

            return new
            {
                items,
                totalCount,
                pageNumber = resultPageNumber,
                pageSize = resultPageSize
            };
        }
    }

    [HttpPut("{userId:guid}/student-profile")]
    public async Task<IActionResult> UpdateStudentProfile(
        Guid institutionId,
        Guid userId,
        [FromBody] UpdateSpeedReadingInstitutionStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(institutionId, cancellationToken))
            return Forbid();
        if (userId == Guid.Empty || request.GradeLevel is < 1 or > 12 || request.TeacherUserId == Guid.Empty)
            return BadRequest(new { code = "ValidationFailed", message = "Öğrenci sınıfı veya öğretmen bilgisi geçersiz." });
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });
        if (!TryGetActor(out var actorId))
            return Unauthorized();

        var result = await studentManagement.UpdateAsync(
            institutionId,
            userId,
            request.GradeLevel,
            request.TeacherUserId,
            actorId,
            DateTime.UtcNow,
            cancellationToken);
        return result switch
        {
            SpeedReadingInstitutionStudentUpdateResult.Updated => NoContent(),
            SpeedReadingInstitutionStudentUpdateResult.InvalidGradeLevel => BadRequest(new
            {
                code = "ValidationFailed",
                message = "Sınıf seviyesi 1 ile 12 arasında olmalıdır."
            }),
            SpeedReadingInstitutionStudentUpdateResult.TeacherAssignmentConflict => Conflict(new
            {
                code = "TeacherStudentAssignment.ReassignmentConflict",
                message = "Öğrenci başka bir öğretmene eş zamanlı atanmış. Listeyi yenileyip tekrar deneyin."
            }),
            SpeedReadingInstitutionStudentUpdateResult.StudentMembershipRequired => Conflict(new
            {
                code = "InstitutionMember.StudentMembershipRequired",
                message = "Öğrencinin kurumda etkin Hızlı Okuma üyeliği bulunmalıdır."
            }),
            SpeedReadingInstitutionStudentUpdateResult.TeacherMembershipRequired => Conflict(new
            {
                code = "InstitutionMember.TeacherMembershipRequired",
                message = "Seçilen öğretmenin kurumda etkin Hızlı Okuma öğretmen üyeliği bulunmalıdır."
            }),
            _ => Conflict(new
            {
                code = "InstitutionMember.ProductRoleRequired",
                message = "Öğrenci ve öğretmen hesaplarının Hızlı Okuma ürün erişimi ve rolleri etkin olmalıdır."
            })
        };
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> SetMember(
        Guid institutionId,
        Guid userId,
        [FromBody] SetSpeedReadingInstitutionMembershipRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(institutionId, cancellationToken))
            return Forbid();
        if (!Enum.IsDefined(request.Role))
            return BadRequest(new { code = "ValidationFailed", message = "Öğrenci veya öğretmen rolü seçilmelidir." });
        if (!await InstitutionExistsAsync(institutionId, cancellationToken))
            return NotFound(new { code = "Institution.NotFound", message = "Aktif Hızlı Okuma kurumu bulunamadı." });
        if (!TryGetActor(out var actorId))
            return Unauthorized();
        if (request.IsActive
            && !await memberEligibility.IsEligibleAsync(userId, request.Role, cancellationToken))
        {
            return Conflict(new
            {
                code = "InstitutionMember.ProductRoleRequired",
                message = "Kullanıcının Hızlı Okuma erişimi ve seçilen ürün rolü bulunmalıdır."
            });
        }

        var updated = await memberships.SetMembershipAsync(
            institutionId,
            userId,
            request.Role,
            request.IsActive,
            actorId,
            DateTime.UtcNow,
            cancellationToken);
        return updated
            ? NoContent()
            : NotFound(new { code = "InstitutionMember.NotFound", message = "Etkinleştirilecek kurum üyeliği bulunamadı." });
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

public sealed record SetSpeedReadingInstitutionMembershipRequest(
    SpeedReadingInstitutionMemberRole Role,
    bool IsActive);

public sealed record UpdateSpeedReadingInstitutionStudentRequest(
    int? GradeLevel,
    Guid? TeacherUserId);
