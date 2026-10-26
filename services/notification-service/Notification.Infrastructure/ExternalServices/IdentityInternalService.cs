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

    public async Task<SupportForwardResult> ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId, CancellationToken cancellationToken = default)
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
                return SupportForwardResult.Rejected;
            }

            using var requestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                $"{_baseUrl}/api/internal/notification/forward-support")
            {
                Content = JsonContent.Create(command)
            };
            requestMessage.Headers.Add(InternalServiceAuthentication.HeaderName, _serviceApiKey);

            using var response = await _httpClient.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.IsSuccessStatusCode) return SupportForwardResult.Sent;
            var status = (int)response.StatusCode;
            _logger.LogWarning("Identity answered {StatusCode} when forwarding support request {SupportRequestId}.", status, supportRequestId);
            return status is >= 400 and < 500 and not 408 and not 429
                ? SupportForwardResult.Rejected
                : SupportForwardResult.Transient;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding support request to Identity Service");
            return SupportForwardResult.Transient;
        }
    }

    public const string SupportCaptchaAction = "support_submit";

    public async Task<bool> VerifySupportCaptchaAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            _logger.LogError("Internal service API key is not configured; the support form CAPTCHA cannot be checked.");
            return false;
        }

        try
        {
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/internal/captcha/verify")
            {
                Content = JsonContent.Create(new { Token = token, Action = SupportCaptchaAction })
            };
            requestMessage.Headers.Add(InternalServiceAuthentication.HeaderName, _serviceApiKey);

            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Identity answered {StatusCode} to the support CAPTCHA check.", (int)response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<CaptchaVerification>(cancellationToken);
            return result?.Accepted == true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            _logger.LogWarning("Support CAPTCHA check could not reach Identity; rejecting the request.");
            return false;
        }
    }

    private sealed record CaptchaVerification(bool Accepted);
}
