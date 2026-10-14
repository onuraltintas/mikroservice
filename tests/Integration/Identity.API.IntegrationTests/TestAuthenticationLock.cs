using Identity.Application.Interfaces;

namespace Identity.API.IntegrationTests;

// Only non-relational tests use this lease; production uses the shared PostgreSQL lock.
internal sealed class TestAuthenticationLock : IAuthenticationLock
{
    public Task<IAsyncDisposable> AcquireAsync(Guid userId, CancellationToken ct) => Task.FromResult<IAsyncDisposable>(new Lease());
    private sealed class Lease : IAsyncDisposable { public ValueTask DisposeAsync() => ValueTask.CompletedTask; }
}
