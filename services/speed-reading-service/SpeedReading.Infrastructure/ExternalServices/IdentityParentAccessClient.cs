using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SpeedReading.Application.Parents;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentityParentAccessClient(HttpClient httpClient, IConfiguration configuration, ILogger<IdentityParentAccessClient> logger)
    : ISpeedReadingParentAccess
{
    public Task<bool> CanReadAsync(Guid parentUserId, Guid studentUserId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
