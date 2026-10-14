using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CourseManagement.Domain;
using EduPlatform.Shared.Contracts.Privacy;
using Microsoft.Extensions.Configuration;

namespace CourseManagement.Infrastructure.Privacy;

public sealed class IdentityCourseManagementErasureReadinessProvider(
    HttpClient httpClient,
    IConfiguration configuration) : ICourseManagementLegalHoldStatusProvider
{
    private const string InternalServiceKeyHeaderName = "X-Internal-Service-Key";

    public async Task<CourseManagementLegalHoldStatus> GetStatusAsync(
        Guid requestId,
        Guid subjectUserId,
        CancellationToken cancellationToken)
    {
        if (requestId == Guid.Empty || subjectUserId == Guid.Empty)
            return CourseManagementLegalHoldStatus.Unknown;

        var identityServiceUrl = configuration["Services:IdentityService"];
        var serviceKey = configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];
        if (string.IsNullOrWhiteSpace(identityServiceUrl)
            || !Uri.TryCreate(identityServiceUrl, UriKind.Absolute, out var identityServiceUri)
            || string.IsNullOrWhiteSpace(serviceKey))
        {
            return CourseManagementLegalHoldStatus.Unknown;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri(identityServiceUri,
                $"api/internal/data-subject-requests/{requestId:D}/erasure-readiness"));
        request.Headers.TryAddWithoutValidation(InternalServiceKeyHeaderName, serviceKey);

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
                return CourseManagementLegalHoldStatus.Unknown;

            var result = await response.Content.ReadFromJsonAsync<DataSubjectRequestErasureReadinessV1>(
                cancellationToken: cancellationToken);
            if (result is null
                || result.RequestId != requestId
                || result.SubjectUserId != subjectUserId
                || !string.Equals(result.RequestType, "Erasure", StringComparison.Ordinal)
                || !string.Equals(result.Scope, "Account", StringComparison.Ordinal)
                || !string.Equals(result.Status, "Processing", StringComparison.Ordinal))
            {
                return CourseManagementLegalHoldStatus.Unknown;
            }

            if (result.HasBlockingLegalHold)
                return CourseManagementLegalHoldStatus.ActiveHold;

            return result.AssessmentIsComplete && result.IsReadyForErasure
                ? CourseManagementLegalHoldStatus.NoActiveHold
                : CourseManagementLegalHoldStatus.Unknown;
        }
        catch (HttpRequestException)
        {
            return CourseManagementLegalHoldStatus.Unknown;
        }
        catch (JsonException)
        {
            return CourseManagementLegalHoldStatus.Unknown;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CourseManagementLegalHoldStatus.Unknown;
        }
    }
}
