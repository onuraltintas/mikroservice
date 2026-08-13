using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class IdentityAuthorizationClient : ICoachingIdentityAuthorizationClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _serviceApiKey;
    private readonly ILogger<IdentityAuthorizationClient> _logger;

    public IdentityAuthorizationClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<IdentityAuthorizationClient> logger)
    {
        _httpClient = httpClient;
        _baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";
        _serviceApiKey = configuration["INTERNAL_SERVICE_API_KEY"]
            ?? configuration["Internal:ServiceApiKey"];
        _logger = logger;
    }

    public async Task<Guid?> AuthorizeTeacherTargetsAsync(
        Guid teacherId,
        IReadOnlyCollection<Guid> studentIds,
        Guid? requestedInstitutionId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        var payload = new
        {
            TeacherId = teacherId,
            StudentIds = studentIds,
            InstitutionId = requestedInstitutionId,
            IsSystemAdministrator = isSystemAdministrator
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/api/internal/coaching/authorize")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add(InternalServiceAuthentication.HeaderName, _serviceApiKey);

        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or
                System.Net.HttpStatusCode.Unauthorized)
            {
                throw new BusinessRuleException(
                    "Authorization.Forbidden",
                    "Teacher ve öğrenci kurum kapsamı uyuşmuyor.");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AuthorizationResponse>(
                cancellationToken: cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException("Identity authorization response was empty.");
            }

            return result.InstitutionId;
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(ex, "Identity authorization dependency failed for teacher {TeacherId}", teacherId);
            throw new InvalidOperationException("Identity authorization service is unavailable.", ex);
        }
    }

    private sealed record AuthorizationResponse(Guid? InstitutionId);
}
