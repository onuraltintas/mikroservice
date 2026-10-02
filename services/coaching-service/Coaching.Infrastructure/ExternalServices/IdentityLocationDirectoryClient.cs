using System.Net.Http.Json;
using Coaching.Application.CatalogAdministration;
using Microsoft.Extensions.Configuration;

namespace Coaching.Infrastructure.ExternalServices;

public sealed class IdentityLocationDirectoryClient(HttpClient httpClient, IConfiguration configuration)
    : ICoachingLocationDirectory
{
    private readonly string baseUrl = (configuration["Services:IdentityService"] ?? "http://localhost:5001").TrimEnd('/');

    public async Task<IReadOnlyList<LocationProvince>> GetProvincesAsync(CancellationToken cancellationToken)
        => await ReadAsync<LocationProvince>("provinces", cancellationToken);

    public async Task<IReadOnlyList<LocationDistrict>> GetDistrictsAsync(string provinceId, CancellationToken cancellationToken)
    {
        ValidateId(provinceId);
        return await ReadAsync<LocationDistrict>($"provinces/{Uri.EscapeDataString(provinceId)}/districts", cancellationToken);
    }

    public async Task<bool> VerifyPairAsync(string provinceId, string districtId, CancellationToken cancellationToken)
    {
        ValidateId(provinceId); ValidateId(districtId);
        var provinces = await GetProvincesAsync(cancellationToken);
        if (provinces.Count(x => x.Id == provinceId) != 1) return false;
        var districts = await GetDistrictsAsync(provinceId, cancellationToken);
        return districts.Count(x => x.Id == districtId) == 1
            && districts.Single(x => x.Id == districtId).ProvinceId == provinceId;
    }

    private async Task<IReadOnlyList<T>> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var response = await httpClient.GetAsync($"{baseUrl}/api/locations/{path}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken)
            ?? throw new HttpRequestException("Shared location directory returned an invalid response.");
    }

    private static void ValidateId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 20 || value != value.Trim())
            throw new ArgumentException("A valid shared location identifier is required.");
    }
}
