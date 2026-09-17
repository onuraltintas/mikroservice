using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Shared.Security.Authorization;

/// <summary>
/// Reads category modes from the identity configuration cache. Missing or invalid
/// values intentionally fail closed to the required mode.
/// </summary>
public sealed class MfaPolicyStore : IMfaPolicyStore
{
    private const string CacheKeyPrefix = "config:";
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MfaPolicyStore> _logger;

    public MfaPolicyStore(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<MfaPolicyStore> logger)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GetModeAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        var normalizedCategory = MfaOperationCategories.Normalize(category);
        var cache = _serviceProvider.GetService<IDistributedCache>();
        if (cache is not null)
        {
            try
            {
                var cached = await cache.GetStringAsync(
                    $"{CacheKeyPrefix}{MfaOperationCategories.ConfigurationKey(normalizedCategory)}",
                    cancellationToken);
                if (MfaPolicyModes.IsKnown(cached))
                {
                    return cached!.Trim().ToLowerInvariant();
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "MFA policy cache could not be read for category {Category}; using fail-closed fallback.",
                    normalizedCategory);
            }
        }

        var configured = _configuration[
            $"Security:Mfa:Categories:{normalizedCategory}"]
            ?? _configuration[
                $"Security__Mfa__Categories__{normalizedCategory}"];
        if (MfaPolicyModes.IsKnown(configured))
        {
            return configured!.Trim().ToLowerInvariant();
        }

        return MfaPolicyModes.Required;
    }
}
