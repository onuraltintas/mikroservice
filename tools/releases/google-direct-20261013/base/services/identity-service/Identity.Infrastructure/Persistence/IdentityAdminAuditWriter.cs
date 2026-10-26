using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.Infrastructure.Persistence;

public sealed class IdentityAdminAuditWriter(IServiceScopeFactory scopeFactory) : IAdminAuditWriter
{
    public async Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.AdminAuditRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
