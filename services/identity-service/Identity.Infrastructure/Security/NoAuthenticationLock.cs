using Identity.Application.Interfaces;

namespace Identity.Infrastructure.Security;

/// <summary>Used only where no lock is registered (unit tests); the application registers PostgreSqlAuthenticationLock.</summary>
internal sealed class NoAuthenticationLock : IAuthenticationLock
{
    public static readonly NoAuthenticationLock Instance = new();

    public Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IAsyncDisposable>(Lease.Instance);

    private sealed class Lease : IAsyncDisposable
    {
        public static readonly Lease Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
