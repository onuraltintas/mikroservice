using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Events.Identity;
using Microsoft.EntityFrameworkCore;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure.Profiles;

public sealed class CoachingInvitationStore(CoachingDbContext db, ICoachingAccountDirectory accounts,
    CoachingProfileStore profiles, TimeProvider clock, ICoachingEventPublisher events, IConfiguration configuration)
{
    public async Task<CoachingInvitation> SendInstitutionAsync(Guid actor, Guid institution, string email,
        string role, Guid? teacherUserId, string? message, CancellationToken ct)
    {
        await profiles.RequireInstitutionAdminAsync(actor, institution, ct);
        if (teacherUserId.HasValue) await RequireTeacher(teacherUserId.Value, institution, ct);
        return await Send(actor, email, role, institution, teacherUserId, true, message, ct);
    }

    public async Task<CoachingInvitation> SendTeacherAsync(Guid actor, string email, string? message, CancellationToken ct)
    {
        var institution = await TeacherInstitution(actor, ct);
        await RequireTeacher(actor, institution, ct);
        return await Send(actor, email, "Student", institution, actor, false, message, ct);
    }

    private async Task<CoachingInvitation> Send(Guid actor, string email, string role, Guid? institution,
        Guid? teacher, bool institutionInvitation, string? message, CancellationToken ct)
    {
        await RequireActiveSubscriptionAsync(institution, teacher, false, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var invitation = CoachingInvitation.Create(actor, email, role, institution, teacher, institutionInvitation, message, now);
        var duplicates = await db.CoachingInvitations.Where(x => x.RecipientEmail == invitation.RecipientEmail
            && x.Role == role && x.InstitutionId == institution && x.TeacherUserId == teacher
            && x.IsInstitutionInvitation == institutionInvitation && x.Status == CoachingInvitationStatus.Pending).ToListAsync(ct);
        if (duplicates.Any(x => x.ExpiresAt > now)) throw CoachingProfileStore.Conflict("Bu kullanıcı için bekleyen davet var.");
        foreach (var old in duplicates) old.Finish(CoachingInvitationStatus.Expired, actor, now);
        if (duplicates.Count > 0) await db.SaveChangesAsync(ct);
        var sender = (await accounts.GetAccountsAsync([actor], ct)).Single(x => x.UserId == actor);
        var recipient = await accounts.FindByEmailAsync(invitation.RecipientEmail, ct);
        db.CoachingInvitations.Add(invitation);
        var publicUrl = CoachingPublicUrl.Get(configuration);
        if (!Uri.TryCreate(publicUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http"))
            throw new InvalidOperationException("Coaching public base URL is invalid.");
        await events.PublishAsync(new InvitationCreatedEvent(invitation.Id, sender.Email, invitation.RecipientEmail,
            recipient?.UserId, invitation.Type, invitation.Message,
            invitation.Role == "Teacher" ? CoachingPublicUrl.StaffPortalLink(configuration) : $"{publicUrl}/coaching-portal/account/settings/profile", now) { Product = "coaching" }, ct);
        await db.SaveChangesAsync(ct);
        return invitation;
    }

    public async Task AcceptAsync(Guid actor, Guid invitationId, CancellationToken ct)
    {
        if (db.Database.IsRelational())
        {
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                var states=db.ChangeTracker.Entries<OutboxState>().ToDictionary(x=>x.Entity.OutboxId,x=>x.State);
                try
                {
                    await AcceptCore(actor, invitationId, ct);
                    await transaction.CommitAsync(ct);
                }
                catch
                {
                    foreach(var entry in db.ChangeTracker.Entries().Where(x=>x.Entity is CoachingInvitation or CoachingStudentProfile
                        or CoachingTeacherProfile or CoachingInstitutionMembership or CoachingTeacherStudentAssignment
                        or Coaching.Domain.Entities.WeeklyDecision or OutboxMessage).ToArray()) entry.State=EntityState.Detached;
                    foreach(var entry in db.ChangeTracker.Entries<OutboxState>().ToArray()) entry.State=states.GetValueOrDefault(entry.Entity.OutboxId,EntityState.Added);
                    throw;
                }
            });
        }
        else await AcceptCore(actor, invitationId, ct);
    }

    private async Task AcceptCore(Guid actor, Guid invitationId, CancellationToken ct)
    {
        var invitation = await Get(invitationId, ct);
        var user = (await accounts.GetAccountsAsync([actor], ct)).SingleOrDefault(x => x.UserId == actor);
        if (user is null || !user.IsEligible(invitation.Role)
            || !user.Email.Equals(invitation.RecipientEmail, StringComparison.OrdinalIgnoreCase)) throw CoachingProfileStore.Forbidden();
        if (invitation.Status == CoachingInvitationStatus.Accepted && invitation.AcceptedByUserId == actor) return;
        RequirePending(invitation);
        if (invitation.IsInstitutionInvitation)
            await profiles.RequireInstitutionAdminAsync(invitation.InviterUserId, invitation.InstitutionId!.Value, ct);
        if (invitation.TeacherUserId.HasValue) await RequireTeacher(invitation.TeacherUserId.Value, invitation.InstitutionId, ct);
        await RequireActiveSubscriptionAsync(invitation.InstitutionId, invitation.TeacherUserId, true, ct);
        var role = invitation.Role == "Teacher" ? CoachingMemberRole.Teacher : CoachingMemberRole.Student;
        var currentInstitution = await db.CoachingInstitutionMemberships.Where(x => x.UserId == actor && x.Role == role && x.IsActive)
            .Select(x => (Guid?)x.InstitutionId).SingleOrDefaultAsync(ct);
        // A closed institution's membership no longer binds its members; the new institution's membership replaces it.
        if (invitation.IsInstitutionInvitation && currentInstitution is { } held && held != invitation.InstitutionId
            && !await accounts.IsActiveInstitutionAsync(held, ct)) currentInstitution = null;
        if (currentInstitution.HasValue && currentInstitution != invitation.InstitutionId
            || !invitation.IsInstitutionInvitation && currentInstitution != invitation.InstitutionId)
            throw CoachingProfileStore.Conflict("Davet mevcut kurum üyeliğinizle uyuşmuyor.");
        await profiles.ProvisionAsync(actor, invitation.Role, ct);
        if (invitation.IsInstitutionInvitation)
            await profiles.SetMembershipAsync(invitation.InviterUserId, invitation.InstitutionId!.Value, actor, role, true, ct);
        if (invitation.TeacherUserId.HasValue)
        {
            await profiles.RequireAssignmentAccountsAsync(invitation.TeacherUserId.Value, actor, invitation.InstitutionId, ct);
            await profiles.UpsertAssignmentAsync(invitation.TeacherUserId.Value, actor, invitation.InstitutionId, null, true, actor, ct);
        }
        var acceptedAt = clock.GetUtcNow().UtcDateTime;
        invitation.Finish(CoachingInvitationStatus.Accepted, actor, acceptedAt);
        // Coaching audit K3: the inviter learns who joined (published in the same transaction through the outbox).
        await events.PublishAsync(new InvitationAcceptedEvent(invitation.Id, invitation.InviterUserId, actor,
            $"{user.FirstName} {user.LastName}".Trim(), invitation.Type, acceptedAt) { Product = "coaching" }, ct);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Coaching audit K3: while subscriptions are enforced, a teacher or institution whose subscription has ended can
    /// neither send invitations nor have a pending invitation accepted (the funded seat is what admits the student).
    /// An institution's subscription covers its teachers; an independent teacher needs a teacher subscription.
    /// </summary>
    private async Task RequireActiveSubscriptionAsync(Guid? institution, Guid? teacher, bool accepting, CancellationToken ct)
    {
        var enforced = await db.CoachingSubscriptionSettings.AsNoTracking().Select(x => (bool?)x.RequireActiveSubscription).FirstOrDefaultAsync(ct) ?? false;
        if (!enforced) return;
        var now = clock.GetUtcNow().UtcDateTime;
        var active = from subscription in db.CoachingSubscriptions.AsNoTracking()
                     join plan in db.CoachingSubscriptionPlans.AsNoTracking() on subscription.PlanId equals plan.Id
                     where subscription.Status == "Active" && subscription.StartDate <= now && subscription.EndDate >= now
                     select new { subscription.UserId, subscription.InstitutionId, plan.Audience };
        bool ok;
        if (institution is { } id) ok = await active.AnyAsync(x => x.InstitutionId == id && x.Audience == "Institution", ct);
        else if (teacher is { } owner) ok = await active.AnyAsync(x => x.UserId == owner && x.Audience == "Teacher", ct);
        else return;
        if (ok) return;
        throw CoachingProfileStore.Conflict(accepting
            ? "Davet eden öğretmenin ya da kurumun aboneliği sona erdiği için bu davet şu an kabul edilemiyor. Davet edenle iletişime geç."
            : "Aboneliğin sona erdiği için yeni davet gönderilemez. Davet göndermek için aboneliğini yenile.");
    }

    public async Task CancelAsync(Guid actor, Guid invitationId, CancellationToken ct)
    {
        var row = await Get(invitationId, ct);
        if (row.InviterUserId != actor) throw CoachingProfileStore.Forbidden();
        RequirePending(row); row.Finish(CoachingInvitationStatus.Cancelled, actor, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }

    public async Task RejectAsync(Guid actor, Guid invitationId, CancellationToken ct)
    {
        var row = await Get(invitationId, ct);
        var user = (await accounts.GetAccountsAsync([actor], ct)).SingleOrDefault(x => x.UserId == actor);
        if (user is null || !user.IsEligible(row.Role) || !user.Email.Equals(row.RecipientEmail, StringComparison.OrdinalIgnoreCase))
            throw CoachingProfileStore.Forbidden();
        RequirePending(row); row.Finish(CoachingInvitationStatus.Rejected, actor, clock.GetUtcNow().UtcDateTime);
        await db.SaveChangesAsync(ct);
    }

    private async Task<CoachingInvitation> Get(Guid id, CancellationToken ct) =>
        await db.CoachingInvitations.SingleOrDefaultAsync(x => x.Id == id, ct)
        ?? throw new KeyNotFoundException("Davet bulunamadı.");

    private void RequirePending(CoachingInvitation row)
    {
        if (row.Status != CoachingInvitationStatus.Pending || row.ExpiresAt <= clock.GetUtcNow().UtcDateTime)
            throw CoachingProfileStore.Conflict("Davet sona ermiş veya sonuçlandırılmış.");
    }

    private Task<Guid?> TeacherInstitution(Guid userId, CancellationToken ct) => db.CoachingInstitutionMemberships
        .Where(x => x.UserId == userId && x.Role == CoachingMemberRole.Teacher && x.IsActive)
        .Select(x => (Guid?)x.InstitutionId).SingleOrDefaultAsync(ct);

    private async Task RequireTeacher(Guid userId, Guid? institution, CancellationToken ct)
    {
        await profiles.RequireAccountAsync(userId, "Teacher", ct);
        if (!await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == userId && x.IsActive, ct)
            || await TeacherInstitution(userId, ct) != institution
            || institution.HasValue && !await accounts.IsActiveInstitutionAsync(institution.Value, ct)) throw CoachingProfileStore.Forbidden();
    }
}
