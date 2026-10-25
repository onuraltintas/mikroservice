using EduPlatform.Shared.Contracts.Coaching;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class CoachingAccountReader(IdentityDbContext db) : ICoachingAccountReader
{
    public async Task<IReadOnlyList<CoachingAccount>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count > 500 || userIds.Any(id => id == Guid.Empty)) throw new ArgumentException("Invalid account scope.");
        var ids = userIds.Distinct().ToArray();
        return await Query(db.Users.AsNoTracking().Where(user => Enumerable.Contains(ids, user.Id))).ToArrayAsync(ct);
    }

    public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct)
    {
        var normalized = Identity.Domain.Entities.User.NormalizeEmail(email);
        return Query(db.Users.AsNoTracking().Where(user => user.Email == normalized)).SingleOrDefaultAsync(ct);
    }

    private static IQueryable<CoachingAccount> Query(IQueryable<Identity.Domain.Entities.User> users) => users.Select(user => new CoachingAccount(
        user.Id, user.FirstName, user.LastName, user.Email, user.IsActive,
        user.ProductAccesses.Any(access => access.Product == PlatformProduct.Coaching && access.IsActive),
        user.Roles.Where(role => !role.Role.IsDeleted
            && (role.Product == PlatformProduct.Coaching || role.Role.Name == "SystemAdmin"))
            .Select(role => role.Role.Name).ToList(), user.LastLoginAt));
}
