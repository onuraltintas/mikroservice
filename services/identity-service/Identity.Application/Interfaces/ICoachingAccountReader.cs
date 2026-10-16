using EduPlatform.Shared.Contracts.Coaching;

namespace Identity.Application.Interfaces;

public interface ICoachingAccountReader
{
    Task<IReadOnlyList<CoachingAccount>> GetAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
    Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}
