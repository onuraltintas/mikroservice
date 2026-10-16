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
        return Ok(rows.Select(x => new { x.Id, inviterEmail = senders.GetValueOrDefault(x.InviterUserId)?.Email,
            type = x.Type, status = x.Status.ToString(), x.Message, x.CreatedAt, x.ExpiresAt }));
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
            ? user : throw new BusinessRuleException("Authorization.Forbidden", "Aktif KoÃ§luk hesabÄ± gereklidir.");
    }
}
