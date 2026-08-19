using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.Extensions.DependencyInjection;

namespace Coaching.Infrastructure.Data;

public sealed class CoachingAdminAuditWriter(IServiceScopeFactory scopeFactory) : IAdminAuditWriter
{
    public async Task WriteAsync(AdminAuditRecord record, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
        await dbContext.AdminAuditRecords.AddAsync(record, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
