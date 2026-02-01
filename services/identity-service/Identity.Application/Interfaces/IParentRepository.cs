using Identity.Domain.Entities;

namespace Identity.Application.Interfaces;

public interface IParentRepository
{
    Task AddAsync(ParentProfile parent, CancellationToken cancellationToken);
    Task<ParentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<ParentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
