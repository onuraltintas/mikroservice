namespace Identity.Application.Interfaces;

/// <summary>
/// Per-account limit on failed password attempts. The account key is the normalised
/// email address, so unknown and existing addresses behave identically and the limit
/// cannot be used to discover which accounts exist.
/// </summary>
public interface ILoginAttemptLimiter
{
    /// <summary>Returns the remaining lock time, or <c>null</c> when attempts are allowed.</summary>
    Task<TimeSpan?> GetLockoutAsync(string email, CancellationToken cancellationToken);

    Task RecordFailureAsync(string email, CancellationToken cancellationToken);

    Task ResetAsync(string email, CancellationToken cancellationToken);
}

/// <summary>Used where no shared cache is configured (unit tests, tools).</summary>
public sealed class NoLoginAttemptLimiter : ILoginAttemptLimiter
{
    public static readonly NoLoginAttemptLimiter Instance = new();

    public Task<TimeSpan?> GetLockoutAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult<TimeSpan?>(null);

    public Task RecordFailureAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ResetAsync(string email, CancellationToken cancellationToken) => Task.CompletedTask;
}
