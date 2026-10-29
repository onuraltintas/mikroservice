using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Content;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Infrastructure.ExternalServices;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingProgressAccess(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionAdministrationAuthorization authorization,
    ISpeedReadingUserDirectory users,
    ICurrentUserService currentUser,
    IdentityProgressAccessClient globalAuthorization,
    IMemoryCache cache) : ISpeedReadingProgressAccess
{
    /// <summary>
    /// Short enough that a revoked administrator loses access within a minute; long enough that one page (scope +
    /// search + several widgets) asks Identity once.
    /// </summary>
    internal static readonly TimeSpan ManagedInstitutionsCacheDuration = TimeSpan.FromSeconds(60);

    public async Task<SpeedReadingProgressAccessScope?> GetScopeAsync(Guid viewerUserId, CancellationToken cancellationToken = default)
    {
        if (!currentUser.IsAuthenticated || viewerUserId == Guid.Empty || currentUser.UserId != viewerUserId)
            return null;
        if (currentUser.Roles.Contains("SystemAdmin"))
        {
            var globalScope = await globalAuthorization.GetScopeAsync(viewerUserId, cancellationToken);
            return globalScope?.IsGlobal == true ? globalScope : null;
        }
        if (currentUser.User?.FindFirst("platform_product")?.Value != "speed-reading"
            || !currentUser.Roles.Any(role => role is "InstitutionAdmin" or "InstitutionOwner"))
            return null;

        // Review H4 (finding 8): one Identity call for the admin's institutions (cached briefly), then only those
        // institutions' student memberships — not every membership on the platform.
        var managed = await cache.GetOrCreateAsync(("speed-reading:managed-institutions", viewerUserId), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ManagedInstitutionsCacheDuration;
            entry.Size = 1;
            return (await authorization.GetManagedInstitutionIdsAsync(viewerUserId, cancellationToken)).ToArray();
        }) ?? [];
        if (managed.Length == 0)
            return new(viewerUserId, false, Array.Empty<Guid>());

        var studentIds = await db.InstitutionMemberships.AsNoTracking()
            .Where(item => item.IsActive
                && item.Role == SpeedReadingInstitutionMemberRole.Student
                && Enumerable.Contains(managed, item.InstitutionId))
            .Select(item => item.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        return new(viewerUserId, false, studentIds);
    }

    public async Task<IReadOnlyCollection<Guid>> SearchStudentUserIdsAsync(Guid viewerUserId, string searchTerm, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return Array.Empty<Guid>();
        var scope = await GetScopeAsync(viewerUserId, cancellationToken);
        if (scope is null)
            return Array.Empty<Guid>();
        var candidates = scope.IsGlobal
            ? await users.GetAudienceUserIdsAsync("Student", cancellationToken)
            : scope.StudentUserIds;
        var term = searchTerm.Trim();
        var matches = new List<Guid>();
        foreach (var batch in candidates.Chunk(500))
        {
            var directory = await users.GetUsersAsync(batch, cancellationToken);
            matches.AddRange(directory.Users.Where(item => item.IsActive && batch.Contains(item.UserId)
                && ($"{item.FirstName} {item.LastName}".Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (item.Email?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)))
                .Select(item => item.UserId));
        }
        return matches.Distinct().ToArray();
    }
}
