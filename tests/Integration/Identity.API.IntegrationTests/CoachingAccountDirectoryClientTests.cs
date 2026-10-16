using System.Net;
using System.Net.Http.Json;
using Coaching.Application.Interfaces;
using Coaching.Infrastructure.ExternalServices;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Security.Services;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAccountDirectoryClientTests
{
    [Fact]
    public async Task AccountBatchesUseServiceKeyAndNeverSendEducationOrCredentials()
    {
        var calls = 0;
        using var http = new HttpClient(new Handler(async request =>
        {
            Assert.Equal("https://identity.example/api/internal/coaching/accounts", request.RequestUri!.ToString());
            Assert.Equal("test-service-key", Assert.Single(request.Headers.GetValues(InternalServiceAuthentication.HeaderName)));
            var batch = await request.Content!.ReadFromJsonAsync<CoachingAccountRequest>();
            Assert.InRange(batch!.UserIds.Count, 1, 500); calls++;
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new CoachingAccountResponse(batch.UserIds.Select(id =>
                new CoachingAccount(id, "Core", "Name", "student@example.test", true, true, ["Student"], null)).ToArray())) };
        }));
        var client = new CoachingAccountDirectoryClient(http, Configuration(), new Representative());
        var ids = Enumerable.Range(0, 1001).Select(_ => Guid.NewGuid()).ToArray();
        var rows = await client.GetAccountsAsync(ids.Concat(ids).ToArray(), default);
        Assert.Equal(3, calls); Assert.Equal(1001, rows.Count);
        Assert.All(rows, row => Assert.True(row.IsEligible("Student")));
    }

    [Fact]
    public async Task MissingScopeAndAccountReturnNoAccessButServiceFailureIsNotAnEmptyRoster()
    {
        using var denied = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))));
        var client = new CoachingAccountDirectoryClient(denied, Configuration(), new Representative());
        Assert.Null(await client.AuthorizeAdminAsync(Guid.NewGuid(), default));
        Assert.Null(await client.FindByEmailAsync("missing@example.test", default));
        Assert.False(await client.IsActiveInstitutionAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAccountsAsync([Guid.NewGuid()], default));
        using var unavailable = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        var outage = new CoachingAccountDirectoryClient(unavailable, Configuration(), new Representative());
        await Assert.ThrowsAsync<HttpRequestException>(() => outage.GetAccountsAsync([Guid.NewGuid()], default));
        var missingKey = new CoachingAccountDirectoryClient(unavailable, new ConfigurationBuilder().Build(), new Representative());
        await Assert.ThrowsAsync<InvalidOperationException>(() => missingKey.GetAccountsAsync([Guid.NewGuid()], default));
    }

    [Fact]
    public async Task SharedAdminInstitutionAndVerifiedRepresentativeFactsAreMappedWithoutProfiles()
    {
        var institution = Guid.NewGuid();
        using var http = new HttpClient(new Handler(request => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = request.Method == HttpMethod.Get ? JsonContent.Create(new { isActive = true }) : JsonContent.Create(new { isGlobal = false, institutionId = institution }) })));
        var representative = new Representative();
        var client = new CoachingAccountDirectoryClient(http, Configuration(), representative);
        Assert.Equal(institution, (await client.AuthorizeAdminAsync(Guid.NewGuid(), default))!.InstitutionId);
        Assert.True(await client.IsActiveInstitutionAsync(institution, default));
        var parent = Guid.NewGuid(); var student = Guid.NewGuid();
        await client.AuthorizeRepresentativeAsync(parent, student, default);
        Assert.Equal((parent, student), representative.Last);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    { ["Services:IdentityService"] = "https://identity.example/", ["Internal:ServiceApiKey"] = "test-service-key" }).Build();
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => respond(request); }
    private sealed class Representative : ICoachingAgreementRepresentativeAuthorizationClient
    {
        public (Guid, Guid) Last;
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeAsync(Guid parent, Guid student, CancellationToken ct)
        { Last = (parent, student); return Task.FromResult<CoachingAgreementRepresentativeAuthorization?>(new(Guid.NewGuid(), "Parent")); }
    }
}
