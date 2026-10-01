using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.ExternalServices;

public sealed class IdentitySharedLegalPageVersionClient(
    HttpClient httpClient,
    IConfiguration configuration) : ISharedLegalPageVersionProvider
{
    private readonly string baseUrl = configuration["Services:IdentityService"] ?? "http://localhost:5001";

    public async Task<int?> GetPublishedVersionAsync(string slug, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug) || !System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9]+(?:-[a-z0-9]+)*$"))
            throw new ArgumentException("A valid legal page slug is required.", nameof(slug));

        try
        {
            using var response = await httpClient.GetAsync(
                $"{baseUrl.TrimEnd('/')}/api/platform/legal-pages/{Uri.EscapeDataString(slug)}",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();
            var envelope = await response.Content.ReadFromJsonAsync<LegalPageEnvelope>(cancellationToken);
            var page = envelope?.Data;
            return envelope?.Success == true
                && page is { IsPublished: true, Version: > 0 }
                && !string.IsNullOrWhiteSpace(page.Content)
                    ? page.Version
                    : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException or System.Text.Json.JsonException)
        {
            throw new SharedLegalPageUnavailableException(exception);
        }
    }

    private sealed record LegalPageEnvelope(bool Success, LegalPage? Data);

    private sealed record LegalPage(bool IsPublished, int Version, string? Content);
}
