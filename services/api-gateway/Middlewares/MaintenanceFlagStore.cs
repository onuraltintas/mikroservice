using System.Collections.Concurrent;
using EduPlatform.Gateway.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace EduPlatform.Gateway.Middlewares;

/// <summary>
/// Reads maintenance flags from the shared cache with a short in-process cache, so the
/// gateway does not make two Redis round trips per request. When Redis fails, the last
/// known state is kept (a service in maintenance stays closed); with no known state the
/// service stays open, matching the previous fail-open behaviour.
/// </summary>
public sealed class MaintenanceFlagStore
{
    private const string CacheKeyPrefix = "config:";

    private readonly IDistributedCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cacheDuration;
    private readonly ILogger<MaintenanceFlagStore> _logger;
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public MaintenanceFlagStore(
        IDistributedCache cache,
        TimeProvider timeProvider,
        IOptions<GatewayMaintenanceOptions> options,
        ILogger<MaintenanceFlagStore> logger)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _cacheDuration = TimeSpan.FromSeconds(options.Value.CacheSeconds);
        _logger = logger;
    }

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        if (_entries.TryGetValue(key, out var cached) && now - cached.ReadAt < _cacheDuration)
        {
            return cached.Enabled;
        }

        try
        {
            var value = await _cache.GetStringAsync(CacheKeyPrefix + key, cancellationToken);
            var enabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
            _entries[key] = new Entry(enabled, now);
            return enabled;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Maintenance flag {Key} could not be read; keeping the last known state", key);
            return cached?.Enabled ?? false;
        }
    }

    private sealed record Entry(bool Enabled, DateTimeOffset ReadAt);
}
