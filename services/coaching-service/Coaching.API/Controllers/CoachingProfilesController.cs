using Asp.Versioning;
using Coaching.Application.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Teacher")]
[Route("api/coaching/profiles/me")]
public sealed class CoachingProfilesController(CoachingDbContext db, CoachingProfileStore profiles,
    ICoachingAccountDirectory accounts, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) => Execute(async () =>
    {
        var userId = await RequireAccount(ct);
        return new CoachingOwnProfile(await profiles.GetStudentAsync(userId, ct), await profiles.GetTeacherAsync(userId, ct),
            await db.CoachingInstitutionMemberships.AsNoTracking().Where(x => x.UserId == userId && x.IsActive).ToArrayAsync(ct));
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
        catch (BusinessRuleException e) when (e.Code == "Profiles.Conflict")
        { return Conflict(new { success = false, code = e.Code, message = e.Message }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Koçluk profili bulunamadı." }); }
    }
}
public sealed record CoachingProfileProvisionRequest(string Role);
