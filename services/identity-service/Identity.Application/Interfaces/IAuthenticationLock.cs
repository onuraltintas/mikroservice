namespace Identity.Application.Interfaces;

public interface IAuthenticationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken);
}
