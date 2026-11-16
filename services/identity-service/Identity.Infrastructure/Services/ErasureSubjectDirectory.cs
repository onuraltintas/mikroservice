using Identity.Application.DataSubjectRequests;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class ErasureSubjectDirectory(IdentityDbContext context) : IErasureSubjectDirectory
{
    private static readonly string SystemAdministrator = UserRole.SystemAdmin.ToString();

    public async Task<ErasureSubject?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
        await context.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new ErasureSubject(
                user.Roles.Any(role => role.Role != null && role.Role.Name == SystemAdministrator),
                user.Email.EndsWith(Identity.Domain.Entities.User.ErasedEmailDomain)))
            .SingleOrDefaultAsync(cancellationToken);
}
