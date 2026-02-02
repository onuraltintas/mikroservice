using Identity.Application.DTOs.Settings;

namespace Identity.Application.Interfaces;

public interface IConfigurationService
{
    Task<List<ConfigurationDto>> GetAllConfigurationsAsync(CancellationToken cancellationToken);
    Task<string?> GetConfigurationValueAsync(string key, CancellationToken cancellationToken);
    Task<string?> GetPublicConfigurationValueAsync(string key, CancellationToken cancellationToken);
    Task<ConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken cancellationToken);
    Task UpdateConfigurationAsync(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken);
    Task DeleteConfigurationAsync(string key, CancellationToken cancellationToken);
    
    // Redis Cache'i manuel temizlemek gerekirse diye
    Task RefreshCacheAsync(CancellationToken cancellationToken);
}
