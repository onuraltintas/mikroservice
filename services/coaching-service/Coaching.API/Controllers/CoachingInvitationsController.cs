using Asp.Versioning;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

[Coaching.API.Security.CoachingProfileExceptionFilter]
[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student,Teacher,InstitutionAdmin,InstitutionOwner,SystemAdmin")]
[Route("api/coaching/invitations")]
public sealed class CoachingInvitationsController(CoachingDbContext db, CoachingInvitationStore invitations,
    ICoachingAccountDirectory accounts, ICurrentUserService actor, TimeProvider clock) : ControllerBase
{
    [HttpGet("sent-pending")]
    public async Task<IActionResult> Sent(CancellationToken ct)
    {
        var user = await Current(ct); var now = clock.GetUtcNow().UtcDateTime;
        return Ok(await db.CoachingInvitations.AsNoTracking().Where(x => x.InviterUserId == user.UserId
            && x.Status == CoachingInvitationStatus.Pending && x.ExpiresAt > now).OrderByDescending(x => x.CreatedAt)
            .Select(x => new { invitationId = x.Id, email = x.RecipientEmail, role = x.Role, x.CreatedAt, x.ExpiresAt }).ToArrayAsync(ct));
    }

    [HttpGet("my-invitations")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var user = await Current(ct); var email = CoachingInvitation.NormalizeEmail(user.Email); var now = clock.GetUtcNow().UtcDateTime;
        var rows = await db.CoachingInvitations.AsNoTracking().Where(x => x.RecipientEmail == email
            && x.Status == CoachingInvitationStatus.Pending && x.ExpiresAt > now).OrderByDescending(x => x.CreatedAt).ToArrayAsync(ct);
        var senders = (await accounts.GetAccountsAsync(rows.Select(x => x.InviterUserId).Distinct().ToArray(), ct)).ToDictionary(x => x.UserId);
        // Live test 13.11.2026: the student saw only the sender's e-mail address; the name says who is inviting.
        static string? FullName(EduPlatform.Shared.Contracts.Coaching.CoachingAccount? account) => account is null ? null : $"{account.FirstName} {account.LastName}".Trim() is { Length: > 0 } name ? name : null;
        int? independentStudents = rows.Any(x => x.Type == "TeacherToInstitution")
            ? await db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x => x.TeacherUserId == user.UserId && x.InstitutionId == null && x.IsActive)
                .Select(x => x.StudentUserId).Distinct().CountAsync(ct) : null;
        var ends = rows.Any(x => x.Type == "StudentToInstitution") ? await IndependentCoachingAsync(user.UserId, now, ct) : null;
        // Live test 16.11.2026 (#39): an institution invitation names the institution, not only the person who sent it.
        var institutionNames = new Dictionary<Guid, string?>();
        foreach (var id in rows.Where(x => x.InstitutionId.HasValue).Select(x => x.InstitutionId!.Value).Distinct())
        {
            // A name that cannot be read is left out; the invitation is still shown.
            try { institutionNames[id] = await accounts.InstitutionNameAsync(id, ct); }
            catch (Exception e) when (e is HttpRequestException or InvalidOperationException) { institutionNames[id] = null; }
        }
        return Ok(rows.Select(x => new { x.Id, inviterEmail = senders.GetValueOrDefault(x.InviterUserId)?.Email,
            inviterName = FullName(senders.GetValueOrDefault(x.InviterUserId)),
            institutionName = x.InstitutionId is { } institution ? institutionNames.GetValueOrDefault(institution) : null,
            type = x.Type, status = x.Status.ToString(), x.Message, x.CreatedAt, x.ExpiresAt,
            // Live test 16.11.2026 (#41, #44): what joining the institution ends, said before the student accepts.
            ends = x.Type == "StudentToInstitution" ? ends : null,
            // #40: a teacher with independent students cannot join yet; the screen says why and how many.
            independentStudents = x.Type == "TeacherToInstitution" ? independentStudents : null }));
    }

    /// <summary>
    /// A student has one teacher at a time (decided 16.11.2026): joining an institution ends the link with an independent teacher,
    /// removes the student from that teacher's coming sessions and closes the open assignments. Null when there is no such teacher.
    /// </summary>
    private async Task<object?> IndependentCoachingAsync(Guid student, DateTime now, CancellationToken ct)
    {
        var teachers = await db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x => x.StudentUserId == student
            && x.InstitutionId == null && x.IsActive).Select(x => x.TeacherUserId).Distinct().ToArrayAsync(ct);
        if (teachers.Length == 0) return null;
        var names = (await accounts.GetAccountsAsync(teachers, ct)).Select(x => $"{x.FirstName} {x.LastName}".Trim())
            .Where(x => x.Length > 0).OrderBy(x => x, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), false)).ToArray();
        var sessions = await db.CoachingSessions.AsNoTracking().CountAsync(x => Enumerable.Contains(teachers, x.TeacherId) && x.InstitutionId == null
            && x.Status == Coaching.Domain.Enums.SessionStatus.Scheduled && x.ScheduledDate > now && x.Attendances.Any(a => a.StudentId == student), ct);
        var assignments = await db.AssignmentStudents.AsNoTracking().CountAsync(x => x.StudentId == student
            && Enumerable.Contains(teachers, x.Assignment.TeacherId) && x.Assignment.InstitutionId == null
            && x.Assignment.Status == Coaching.Domain.Enums.AssignmentStatus.Active
            && (x.Status == Coaching.Domain.Enums.StudentAssignmentStatus.Assigned || x.Status == Coaching.Domain.Enums.StudentAssignmentStatus.InProgress), ct);
        return new { teachers = names, sessions, assignments };
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<IActionResult> Accept(Guid id, CancellationToken ct) { await invitations.AcceptAsync((await Current(ct)).UserId, id, ct); return NoContent(); }
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct) { await invitations.RejectAsync((await Current(ct)).UserId, id, ct); return NoContent(); }
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct) { await invitations.CancelAsync((await Current(ct)).UserId, id, ct); return NoContent(); }

    private async Task<EduPlatform.Shared.Contracts.Coaching.CoachingAccount> Current(CancellationToken ct)
    {
        var id = actor.UserId ?? Guid.Empty;
        var user = (await accounts.GetAccountsAsync([id], ct)).SingleOrDefault(x => x.UserId == id);
        return user is not null && user.IsActive && (user.HasCoachingAccess || user.IsSystemAdministrator)
            ? user : throw new BusinessRuleException("Authorization.Forbidden", "Aktif Koçluk hesabı gereklidir.");
    }
}
