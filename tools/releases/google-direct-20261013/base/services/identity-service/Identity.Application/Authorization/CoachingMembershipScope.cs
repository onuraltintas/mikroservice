using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;

namespace Identity.Application.Authorization;

public static class CoachingMembershipScope
{
    public static async Task<Guid?> ResolveAsync(ICurrentUserService actor,
        IInstitutionRepository institutions, Guid? requestedInstitutionId, CancellationToken cancellationToken)
    {
        if (actor.UserId is not { } actorId) return null;
        if (requestedInstitutionId is { } institutionId)
        {
            if (institutionId == Guid.Empty || !actor.Roles.Contains("SystemAdmin", StringComparer.OrdinalIgnoreCase))
                return null;
            var institution = await institutions.GetByIdAsync(institutionId, cancellationToken);
            return institution?.IsActive == true ? institutionId : null;
        }

        return await institutions.GetInstitutionIdByAdminIdAsync(actorId, PlatformProduct.Coaching, cancellationToken);
    }
}
