using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class IdentityAuthorizationClient : ICoachingIdentityAuthorizationClient, ICoachingIdentityReportClient
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

    public async Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken)
    {
        if (viewerUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Oturum açmış kullanıcı bulunamadı.");
        }

        EnsureServiceApiKey();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/api/internal/coaching/authorize-admin")
        {
            Content = JsonContent.Create(new { ViewerUserId = viewerUserId })
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
                    "Coaching yönetim kapsamına erişim yetkiniz yok.");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AdminAuthorizationResponse>(
                cancellationToken: cancellationToken);
            return result is null
                ? throw new InvalidOperationException("Identity admin authorization response was empty.")
                : new CoachingAdminAccessScope(result.IsGlobal, result.InstitutionId);
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(
                ex,
                "Identity coaching admin authorization failed for viewer {ViewerUserId}",
                viewerUserId);
            throw new InvalidOperationException("Identity authorization service is unavailable.", ex);
        }
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

    public async Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        if (viewerUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Oturum açılmış kullanıcı bulunamadı.");
        }

        if (studentIds.Count == 0 || studentIds.Count > 100)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Öğrenci erişim kapsamı geçersiz.");
        }

        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        var payload = new
        {
            ViewerUserId = viewerUserId,
            StudentIds = studentIds
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/api/internal/coaching/authorize-student-read")
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
                    "Öğrenci verisine erişim yetkiniz yok.");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<StudentReadAuthorizationResponse>(
                cancellationToken: cancellationToken);
            if (result is null)
            {
                throw new InvalidOperationException("Identity student authorization response was empty.");
            }

            return result.AllowedStudentUserIds ?? Array.Empty<Guid>();
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(
                ex,
                "Identity student read authorization failed for viewer {ViewerUserId}",
                viewerUserId);
            throw new InvalidOperationException("Identity authorization service is unavailable.", ex);
        }
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        CancellationToken cancellationToken)
    {
        if (viewerUserId == Guid.Empty || institutionId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Rapor kapsamı geçersiz.");
        }

        if (gradeLevel is < 1 or > 12)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Rapor sınıf seviyesi geçersiz.");
        }

        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        var payload = new
        {
            ViewerUserId = viewerUserId,
            InstitutionId = institutionId,
            GradeLevel = gradeLevel
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/api/internal/coaching/report-students")
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
                    "Kurum raporu kapsamına erişim yetkiniz yok.");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ReportStudentResponse>(
                cancellationToken: cancellationToken);
            return result?.StudentUserIds ?? Array.Empty<Guid>();
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(
                ex,
                "Identity report student scope failed for institution {InstitutionId}",
                institutionId);
            throw new InvalidOperationException("Identity report scope service is unavailable.", ex);
        }
    }

    public async Task<CoachingStudentReportPage> GetActiveStudentPageAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (viewerUserId == Guid.Empty || institutionId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Rapor kapsamı geçersiz.");
        }

        if (gradeLevel is < 1 or > 12)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Rapor sınıf seviyesi geçersiz.");
        }

        if (pageNumber is < 1 or > 1000 || pageSize is < 1 or > 100)
        {
            throw new BusinessRuleException(
                "Authorization.Forbidden",
                "Rapor sayfalama kapsamı geçersiz.");
        }

        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }

        var payload = new
        {
            ViewerUserId = viewerUserId,
            InstitutionId = institutionId,
            GradeLevel = gradeLevel,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/api/internal/coaching/report-student-page")
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
                    "Kurum raporu kapsamına erişim yetkiniz yok.");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<ReportStudentPageResponse>(
                cancellationToken: cancellationToken);
            return result is null
                ? throw new InvalidOperationException("Identity report student page response was empty.")
                : new CoachingStudentReportPage(
                    result.StudentUserIds ?? Array.Empty<Guid>(),
                    result.TotalCount);
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogError(
                ex,
                "Identity report student page scope failed for institution {InstitutionId}",
                institutionId);
            throw new InvalidOperationException("Identity report scope service is unavailable.", ex);
        }
    }

    private sealed record AuthorizationResponse(Guid? InstitutionId);
    private sealed record AdminAuthorizationResponse(bool IsGlobal, Guid? InstitutionId);
    private sealed record StudentReadAuthorizationResponse(Guid[]? AllowedStudentUserIds);
    private sealed record ReportStudentResponse(Guid[]? StudentUserIds);
    private sealed record ReportStudentPageResponse(Guid[]? StudentUserIds, int TotalCount);

    private void EnsureServiceApiKey()
    {
        if (string.IsNullOrWhiteSpace(_serviceApiKey))
        {
            throw new InvalidOperationException("Internal service API key is not configured.");
        }
    }
}
