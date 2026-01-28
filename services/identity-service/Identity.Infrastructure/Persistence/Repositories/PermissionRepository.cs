using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class PermissionRepository : IPermissionRepository
{
    private readonly IdentityDbContext _context;

    public PermissionRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Permission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .IgnoreQueryFilters() // To support restoring deleted ones
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<Permission?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .FirstOrDefaultAsync(x => x.Key == key, cancellationToken);
    }

    public async Task<List<Permission>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Permissions
            .IgnoreQueryFilters() // We want to see pasive ones in the list too if needed
            .OrderBy(x => x.Group)
            .ThenBy(x => x.Key)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        await _context.Permissions.AddAsync(permission, cancellationToken);
    }

    public Task DeleteAsync(Permission permission, CancellationToken cancellationToken = default)
    {
        _context.Permissions.Remove(permission);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return await _context.Permissions.AnyAsync(x => x.Key == key, cancellationToken);
    }
}
