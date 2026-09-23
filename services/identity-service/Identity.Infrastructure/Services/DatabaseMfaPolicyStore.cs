using EduPlatform.Shared.Security.Authorization;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public sealed class DatabaseMfaPolicyStore : IMfaPolicyStore
{
    private readonly IdentityDbContext _context;
    private readonly ILogger<DatabaseMfaPolicyStore> _logger;

    public DatabaseMfaPolicyStore(
        IdentityDbContext context,
        ILogger<DatabaseMfaPolicyStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<string> GetModeAsync(
        string category,
        CancellationToken cancellationToken = default)
    {
        if (!MfaOperationCategories.IsKnown(category))
        {
            return MfaPolicyModes.Required;
        }

        var key = MfaOperationCategories.ConfigurationKey(category);
        try
        {
            var value = await _context.Configurations
                .AsNoTracking()
                .Where(config => config.Key.ToLower() == key
                    && config.DataType != ConfigurationDataType.Secret)
                .Select(config => config.Value)
                .FirstOrDefaultAsync(cancellationToken);

            if (MfaPolicyModes.IsKnown(value))
            {
                return value!.Trim().ToLowerInvariant();
            }

            _logger.LogWarning("MFA policy is missing or invalid for {Category}.", category);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "MFA policy database read failed for {Category}.", category);
        }

        return MfaPolicyModes.Required;
    }
}
