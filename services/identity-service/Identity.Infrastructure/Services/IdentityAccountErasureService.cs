using Identity.Application.DataSubjectRequests;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class IdentityAccountErasureService(IdentityDbContext context)
    : IIdentityAccountErasureService
{
    public async Task ExecuteAsync(Guid subjectUserId, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .SingleOrDefaultAsync(item => item.Id == subjectUserId, cancellationToken);
        if (user is null)
            return;

        var relationships = await context.ParentStudentRelationships
            .Where(item => item.ParentUserId == subjectUserId
                || item.StudentUserId == subjectUserId)
            .ToListAsync(cancellationToken);
        var invitations = await context.Invitations
            .Where(item => item.InviterId == subjectUserId
                || item.InviteeUserId == subjectUserId)
            .ToListAsync(cancellationToken);

        context.ParentStudentRelationships.RemoveRange(relationships);
        context.Invitations.RemoveRange(invitations);
        context.InstitutionAdmins.RemoveRange(
            await context.InstitutionAdmins.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));
        context.UserRoles.RemoveRange(
            await context.UserRoles.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));
        context.RefreshTokens.RemoveRange(
            await context.RefreshTokens.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));
        context.UserLogins.RemoveRange(
            await context.UserLogins.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));
        context.StudentProfiles.RemoveRange(
            await context.StudentProfiles.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));
        context.TeacherProfiles.RemoveRange(
            await context.TeacherProfiles.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));
        context.ParentProfiles.RemoveRange(
            await context.ParentProfiles.Where(item => item.UserId == subjectUserId).ToListAsync(cancellationToken));

        user.AnonymizeForErasure();
    }
}
