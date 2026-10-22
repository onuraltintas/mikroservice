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
    [HttpGet("time-zone")]
    [Authorize(Roles = "Student")]
    public Task<IActionResult> GetTimeZone(CancellationToken ct) => Execute(async () =>
    {
        var id = await RequireAccount(ct);
        var hours = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == id, ct);
        var draft = await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == id, ct);
        return new CoachingTimeZoneView(hours?.TimeZoneId ?? draft?.TimeZoneId, hours?.Version, draft?.Version);
    });

    [HttpPut("time-zone")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> UpdateTimeZone(CoachingTimeZoneUpdate request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.TimeZoneId) || request.TimeZoneId.Length > 100)
            return BadRequest(new { message = "Geçerli bir saat dilimi seçin." });
        var zone = request.TimeZoneId.Trim();
        try
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
            if (!timeZone.HasIanaId && zone != "UTC")
                return BadRequest(new { message = "Europe/Istanbul gibi bölge/şehir biçiminde bir saat dilimi seçin." });
        }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return BadRequest(new { message = "Saat dilimi tanınmadı." }); }
        return await Execute(async () =>
        {
            var id = await RequireAccount(ct);
            async Task<CoachingTimeZoneView> Save()
            {
                var hours = await db.StudyAvailability.SingleOrDefaultAsync(x => x.StudentId == id, ct);
                var draft = await db.Set<WeeklyStudyDraft>().SingleOrDefaultAsync(x => x.StudentId == id, ct);
                if (request.AvailabilityVersion != hours?.Version || request.DraftVersion != draft?.Version)
                    throw new BusinessRuleException("Profiles.Conflict", "Çalışma düzenin değişti. Ayarları yeniden yükleyip tekrar dene.");
                if (hours is null) db.StudyAvailability.Add(hours = StudyAvailability.Create(id, zone));
                else hours.ReplacePreferences(zone, hours.Windows);
                if (draft is not null) draft.Replace(zone, draft.Slots);
                try { await db.SaveChangesAsync(ct); }
                catch (Exception e) when (e is DbUpdateConcurrencyException or ConcurrencyException
                    || e is DbUpdateException { InnerException: Npgsql.PostgresException { SqlState: "23505" } })
                { throw new BusinessRuleException("Profiles.Conflict", "Çalışma düzenin değişti. Ayarları yeniden yükleyip tekrar dene."); }
                return new CoachingTimeZoneView(zone, hours.Version, draft?.Version);
            }
            if (!db.Database.IsRelational()) return await Save();
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var key = "coaching-study-plan:" + id;
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key},0))", ct);
                var result = await Save();
                await transaction.CommitAsync(ct);
                return result;
            });
        });
    }

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
            showSessions = managed || await db.SessionAttendances.AsNoTracking().AnyAsync(x => x.StudentId == userId, ct)
        };
    });

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
public sealed record CoachingTimeZoneView(string? TimeZoneId, int? AvailabilityVersion, int? DraftVersion);
public sealed record CoachingTimeZoneUpdate(string TimeZoneId, int? AvailabilityVersion, int? DraftVersion);
