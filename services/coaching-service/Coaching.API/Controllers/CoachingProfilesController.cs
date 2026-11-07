using Asp.Versioning;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Coaching.Domain.Entities;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Teacher")]
[Route("api/coaching/profiles/me")]
public sealed class CoachingProfilesController(CoachingDbContext db, CoachingProfileStore profiles,
    ICoachingAccountDirectory accounts, ICurrentUserService currentUser) : ControllerBase
{
    // E3 #34 (user decision 06.11.2026): the time-zone setting (GET/PUT time-zone) is gone; everyone is on Europe/Istanbul.

    [HttpGet("navigation")]
    [Authorize(Roles = "Student")]
    public Task<IActionResult> Navigation(CancellationToken ct) => Execute(async () =>
    {
        var userId = await RequireAccount(ct);
        var managed = await db.CoachingTeacherStudentAssignments.AsNoTracking()
            .AnyAsync(x => x.StudentUserId == userId && x.IsActive, ct)
            || await db.CoachingInstitutionMemberships.AsNoTracking().AnyAsync(x => x.UserId == userId && x.IsActive, ct);
        return new
        {
            showFees = managed || await db.CoachingFeeAgreements.AsNoTracking().AnyAsync(x => x.StudentId == userId, ct),
            showAssignments = managed || await db.AssignmentStudents.AsNoTracking().AnyAsync(x => x.StudentId == userId, ct),
            showSessions = managed || await db.SessionAttendances.AsNoTracking().AnyAsync(x => x.StudentId == userId, ct),
            // Paket F #20: when the student's profile was made (UTC); "Panelin yenilendi" is only for students from before the new menu.
            registeredAt = await db.CoachingStudentProfiles.AsNoTracking().Where(x => x.UserId == userId)
                .Select(x => (DateTime?)x.CreatedAt).FirstOrDefaultAsync(ct) is { } created ? DateTime.SpecifyKind(created, DateTimeKind.Utc) : (DateTime?)null
        };
    });

    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) => Execute(async () =>
    {
        var userId = await RequireAccount(ct);
        return new CoachingOwnProfile(await profiles.GetStudentAsync(userId, ct), await profiles.GetTeacherAsync(userId, ct),
            await db.CoachingInstitutionMemberships.AsNoTracking().Where(x => x.UserId == userId && x.IsActive).ToArrayAsync(ct))
            { ExamAsksDiploma = await profiles.ExamAsksDiplomaAsync(userId, ct) };
    });

    [HttpPost("provision")]
    [RequestSizeLimit(4096)]
    public Task<IActionResult> Provision([FromBody] CoachingProfileProvisionRequest request, CancellationToken ct) => Execute(async () =>
    {
        var userId = await RequireAccount(ct);
        await profiles.ProvisionAsync(userId, request.Role, ct);
        return new { userId };
    });

    [HttpPut("student")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(16384)]
    public Task<IActionResult> UpdateStudent([FromBody] UpdateCoachingStudentProfile request, CancellationToken ct) =>
        Execute(async () => await profiles.UpdateStudentAsync(await RequireAccount(ct), request, ct));

    /// <summary>E4 #23: what saving this education would remove or leave not fitting (diploma grade, exam, target); nothing is saved.</summary>
    [HttpPost("student/impact")]
    [Authorize(Roles = "Student")]
    [RequestSizeLimit(1024)]
    public Task<IActionResult> StudentImpact([FromBody] StudentEducationImpactRequest request, CancellationToken ct) =>
        Execute(async () => await profiles.PreviewEducationAsync(await RequireAccount(ct), request.EducationStatus, request.GradeLevel, ct));

    [HttpPut("teacher")]
    [Authorize(Roles = "Teacher")]
    [RequestSizeLimit(16384)]
    public Task<IActionResult> UpdateTeacher([FromBody] UpdateCoachingTeacherProfile request, CancellationToken ct) =>
        Execute(async () => await profiles.UpdateTeacherAsync(await RequireAccount(ct), request, ct));

    private async Task<Guid> RequireAccount(CancellationToken ct)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        if (userId == Guid.Empty) throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
        var account = (await accounts.GetAccountsAsync([userId], ct)).SingleOrDefault(x => x.UserId == userId);
        if (account is null || !account.IsEligible("Student") && !account.IsEligible("Teacher"))
            throw new BusinessRuleException("Authorization.Forbidden", "Aktif Koçluk hesabı gereklidir.");
        return userId;
    }

    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization."))
        { return StatusCode(403, new { success = false, code = e.Code, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "Profiles.LocationsUnavailable")
        { return StatusCode(503, new { success = false, code = e.Code, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "Profiles.Conflict")
        { return Conflict(new { success = false, code = e.Code, message = e.Message }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Koçluk profili bulunamadı." }); }
    }
}
public sealed record CoachingProfileProvisionRequest(string Role);
