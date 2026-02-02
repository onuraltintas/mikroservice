using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Identity.Infrastructure.Services;

public class ConfigurationService : IConfigurationService
{
    private readonly IdentityDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ConfigurationService> _logger;
    private const string CacheKeyPrefix = "config:";

    public ConfigurationService(IdentityDbContext context, IDistributedCache cache, ILogger<ConfigurationService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<List<ConfigurationDto>> GetAllConfigurationsAsync(CancellationToken cancellationToken)
    {
        return await _context.Configurations
            .AsNoTracking()
            .Select(c => new ConfigurationDto
            {
                Id = c.Id,
                Key = c.Key.ToLower().Trim(),
                Value = c.Value,
                Description = c.Description,
                DataType = c.DataType,
                IsPublic = c.IsPublic,
                Group = c.Group
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetConfigurationValueAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = key.ToLower().Trim();
        var cacheKey = $"{CacheKeyPrefix}{normalizedKey}";

        var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedValue)) return cachedValue;

        var config = await _context.Configurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key.ToLower() == normalizedKey, cancellationToken);

        if (config == null) return null;

        await _cache.SetStringAsync(cacheKey, config.Value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        }, cancellationToken);

        return config.Value;
    }

    public async Task<string?> GetPublicConfigurationValueAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = key.ToLower().Trim();
        var cacheKey = $"{CacheKeyPrefix}public:{normalizedKey}";

        var cachedValue = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedValue)) return cachedValue;

        // Check if config exists AND IS PUBLIC
        var config = await _context.Configurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Key.ToLower() == normalizedKey && c.IsPublic, cancellationToken);

        if (config == null) return null;

        await _cache.SetStringAsync(cacheKey, config.Value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        }, cancellationToken);

        return config.Value;
    }

    public async Task<ConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken cancellationToken)
    {
        var normalizedKey = request.Key.ToLower().Trim();
        
        var existing = await _context.Configurations
            .FirstOrDefaultAsync(c => c.Key.ToLower() == normalizedKey, cancellationToken);

        if (existing != null)
        {
            _logger.LogInformation("Updating existing configuration: {Key}", normalizedKey);
            existing.UpdateValue(request.Value, request.IsPublic);
            existing.UpdateMetadata(request.Description, request.Group, request.DataType);
            
            await _context.SaveChangesAsync(cancellationToken);
            await UpdateCacheAsync(normalizedKey, request.Value, cancellationToken);
            return MapToDto(existing);
        }

        _logger.LogInformation("Creating new configuration: {Key}", normalizedKey);
        var config = SystemConfiguration.Create(
            normalizedKey,
            request.Value,
            request.Description,
            request.DataType,
            request.Group,
            request.IsPublic);

        await _context.Configurations.AddAsync(config, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        await UpdateCacheAsync(normalizedKey, config.Value, cancellationToken);

        return MapToDto(config);
    }

    public async Task UpdateConfigurationAsync(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken)
    {
        var normalizedKey = key.ToLower().Trim();
        var config = await _context.Configurations
            .FirstOrDefaultAsync(c => c.Key.ToLower() == normalizedKey, cancellationToken);

        if (config == null) throw new Exception($"Configuration '{normalizedKey}' not found.");

        config.UpdateValue(request.Value);
        await _context.SaveChangesAsync(cancellationToken);
        await UpdateCacheAsync(normalizedKey, request.Value, cancellationToken);
    }

    public async Task DeleteConfigurationAsync(string key, CancellationToken cancellationToken)
    {
        var normalizedKey = key.ToLower().Trim();
        var config = await _context.Configurations
            .FirstOrDefaultAsync(c => c.Key.ToLower() == normalizedKey, cancellationToken);

        if (config == null) return;

        _context.Configurations.Remove(config);
        await _context.SaveChangesAsync(cancellationToken);
        
        // Remove from both caches
        await _cache.RemoveAsync($"{CacheKeyPrefix}{normalizedKey}", cancellationToken);
        await _cache.RemoveAsync($"{CacheKeyPrefix}public:{normalizedKey}", cancellationToken);
    }

    public async Task RefreshCacheAsync(CancellationToken cancellationToken)
    {
        var configs = await _context.Configurations.ToListAsync(cancellationToken);
        foreach (var config in configs)
        {
            await UpdateCacheAsync(config.Key.ToLower().Trim(), config.Value, cancellationToken);
        }
    }

    private async Task UpdateCacheAsync(string key, string value, CancellationToken ct)
    {
        // We only update the main cache here. Public cache expires or is removed on delete.
        // It's safer to invalidate public cache on update.
        await _cache.SetStringAsync($"{CacheKeyPrefix}{key}", value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
        }, ct);
        
        // Invalidate public cache for this key
         await _cache.RemoveAsync($"{CacheKeyPrefix}public:{key}", ct);
    }

    private static ConfigurationDto MapToDto(SystemConfiguration c) => new()
    {
        Id = c.Id,
        Key = c.Key.ToLower().Trim(),
        Value = c.Value,
        Description = c.Description,
        DataType = c.DataType,
        IsPublic = c.IsPublic,
        Group = c.Group
    };
}
