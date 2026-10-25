using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class DataSubjectRequestExecutionRepository(IdentityDbContext context)
    : IDataSubjectRequestExecutionRepository
{
    public Task<bool> ExistsAsync(
        Guid requestId,
        string serviceName,
        CancellationToken cancellationToken) =>
        context.DataSubjectRequestExecutionResults.AnyAsync(
            result => result.RequestId == requestId && result.ServiceName == serviceName,
            cancellationToken);

    public Task AddAsync(
        DataSubjectRequestExecutionResult result,
        CancellationToken cancellationToken)
    {
        context.DataSubjectRequestExecutionResults.Add(result);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<DataSubjectRequestExecutionResult>> GetByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken) =>
        await context.DataSubjectRequestExecutionResults
            .AsNoTracking()
            .Where(result => result.RequestId == requestId)
            .OrderBy(result => result.ServiceName)
            .ToListAsync(cancellationToken);
}
