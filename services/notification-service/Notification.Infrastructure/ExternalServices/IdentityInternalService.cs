using System.Net.Http.Json;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Notification.Application.Commands.SubmitSupportRequest;
using Notification.Application.Interfaces;

namespace Notification.Infrastructure.ExternalServices;

public class IdentityInternalService : IIdentityInternalService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _serviceApiKey;
    private readonly ILogger<IdentityInternalService> _logger;

    public IdentityInternalService(HttpClient httpClient, IConfiguration configuration, ILogger<IdentityInternalService> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        _serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        _logger = logger;
    }

    public async Task<bool> ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId, CancellationToken cancellationToken = default)
    {
        var command = new
        {
            SupportRequestId = supportRequestId,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Subject,
            request.Message
        };

        try
        {
            if (string.IsNullOrWhiteSpace(_serviceApiKey))
            {
                _logger.LogError("Internal service API key is not configured; support request {SupportRequestId} was not forwarded.", supportRequestId);
                return false;
            }

            using var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/api/internal/notification/forward-support")
            {
                Content = JsonContent.Create(command)
            };
            requestMessage.Headers.Add(InternalServiceAuthentication.HeaderName, _serviceApiKey);

            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding support request to Identity Service");
            return false;
        }
    }
}
