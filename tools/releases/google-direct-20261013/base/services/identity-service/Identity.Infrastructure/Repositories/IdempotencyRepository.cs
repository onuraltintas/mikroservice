using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public sealed class IdempotencyRepository(IdentityDbContext context) : IIdempotencyRepository
{
    public Task<IdempotencyRecord?> GetAsync(
        string scope,
        string key,
        CancellationToken cancellationToken)
    {
        return context.IdempotencyRecords
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Scope == scope && record.Key == key,
                cancellationToken);
    }

    public Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        return context.IdempotencyRecords.AddAsync(record, cancellationToken).AsTask();
    }
}
