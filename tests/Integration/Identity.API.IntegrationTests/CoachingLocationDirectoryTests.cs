using System.Net;
using System.Net.Http.Json;
using Coaching.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class CoachingLocationDirectoryTests
{
    [Fact]
    public async Task VerifiesTheDistrictOnlyInsideTheSelectedProvince()
    {
        var urls = new List<string>();
        using var http = new HttpClient(new Handler(request =>
        {
            urls.Add(request.RequestUri!.AbsolutePath);
            return new(HttpStatusCode.OK) { Content = request.RequestUri.AbsolutePath.EndsWith("/districts")
                ? JsonContent.Create(new[] { new { id = "TUR006007", provinceId = "TUR006", name = "Çankaya" } })
                : JsonContent.Create(new[] { new { id = "TUR006", name = "Ankara" } }) };
        }));
        var client = new IdentityLocationDirectoryClient(http, Config());
        Assert.True(await client.VerifyPairAsync("TUR006", "TUR006007", default));
        Assert.Equal(["/api/locations/provinces", "/api/locations/provinces/TUR006/districts"], urls);
    }

    [Fact]
    public async Task RejectsCrossProvinceResponseEvenWhenDistrictIdMatches()
    {
        using var http = new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("/districts")
                ? JsonContent.Create(new[] { new { id = "district", provinceId = "other", name = "District" } })
                : JsonContent.Create(new[] { new { id = "province", name = "Province" } })
        }));
        Assert.False(await new IdentityLocationDirectoryClient(http, Config()).VerifyPairAsync("province", "district", default));
    }

    [Fact]
    public async Task UnavailableIdentityCannotBeTreatedAsSuccessfulVerification()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.ServiceUnavailable)));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new IdentityLocationDirectoryClient(http, Config()).VerifyPairAsync("province", "district", default));
    }

    private static IConfiguration Config() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Services:IdentityService"] = "http://identity.test/" }).Build();

    [Theory]
    [InlineData("")]
    [InlineData(" province ")]
    [InlineData("identifier-longer-than-twenty")]
    public async Task InvalidIdentifiersAreRejectedBeforeSendingARequest(string province)
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Must not call Identity")));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            new IdentityLocationDirectoryClient(http, Config()).VerifyPairAsync(province, "district", default));
    }

    [Fact]
    public async Task DuplicateProvinceIdentifiersAreNotAccepted()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new[] { new { id = "province", name = "A" }, new { id = "province", name = "B" } })
        }));
        Assert.False(await new IdentityLocationDirectoryClient(http, Config()).VerifyPairAsync("province", "district", default));
    }

    [Fact]
    public async Task CallerCancellationIsPreserved()
    {
        using var http = new HttpClient(new Handler(_ => throw new InvalidOperationException("Must not call Identity")));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new IdentityLocationDirectoryClient(http, Config()).VerifyPairAsync("province", "district", cancellation.Token));
    }

    [Fact]
    public async Task NullDirectoryResponseIsNotAnEmptySuccessfulDirectory()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.OK) { Content = JsonContent.Create<object?>(null) }));
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new IdentityLocationDirectoryClient(http, Config()).GetProvincesAsync(default));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDuplicateDistrictIdentifiersAreRejected(bool duplicate)
    {
        using var http = new HttpClient(new Handler(request => new(HttpStatusCode.OK)
        {
            Content = request.RequestUri!.AbsolutePath.EndsWith("/districts")
                ? JsonContent.Create(duplicate
                    ? new[] { new { id = "district", provinceId = "province", name = "A" }, new { id = "district", provinceId = "province", name = "B" } }
                    : Array.Empty<object>())
                : JsonContent.Create(new[] { new { id = "province", name = "Province" } })
        }));
        Assert.False(await new IdentityLocationDirectoryClient(http, Config()).VerifyPairAsync("province", "district", default));
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}
