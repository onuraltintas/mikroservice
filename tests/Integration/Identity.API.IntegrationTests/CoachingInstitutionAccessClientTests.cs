using System.Net;
using System.Net.Http.Json;
using Coaching.Infrastructure.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingInstitutionAccessClientTests
{
    [Theory]
    [InlineData(HttpStatusCode.OK, true, true)]
    [InlineData(HttpStatusCode.OK, false, false)]
    [InlineData(HttpStatusCode.Forbidden, true, false)]
    [InlineData(HttpStatusCode.Unauthorized, true, false)]
    public async Task ValidatesMembershipThroughServiceAuthenticatedEndpoint(HttpStatusCode status, bool active, bool expected)
    {
        var institution = Guid.NewGuid(); var student = Guid.NewGuid();
        using var http = new HttpClient(new Handler(async request => {
            Assert.Equal("http://identity.test/api/internal/coaching/institution-student", request.RequestUri!.ToString());
            Assert.Equal(HttpMethod.Post, request.Method); Assert.Contains("local-test-key", request.Headers.GetValues(EduPlatform.Shared.Security.Services.InternalServiceAuthentication.HeaderName));
            var body = await request.Content!.ReadFromJsonAsync<System.Text.Json.JsonElement>();
            Assert.Equal(institution, body.GetProperty("institutionId").GetGuid()); Assert.Equal(student, body.GetProperty("studentId").GetGuid());
            return new(status) { Content = JsonContent.Create(new { isActive = active }) };
        }));
        Assert.Equal(expected, await Client(http).IsActiveInstitutionStudentAsync(institution, student, default));
    }

    [Fact]
    public async Task DependencyFailureNeverGrantsAccess()
    {
        using var http = new HttpClient(new Handler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(http).IsActiveInstitutionStudentAsync(Guid.NewGuid(), Guid.NewGuid(), default));
    }

    [Fact]
    public void InternalMembershipEndpointRequiresServiceKeyAndBoundedBody()
    {
        var method = typeof(Identity.API.Controllers.InternalCoachingController).GetMethod("IsInstitutionStudent")!;
        Assert.NotEmpty(method.GetCustomAttributes(typeof(Identity.API.InternalServiceKeyAttribute), true));
        Assert.NotEmpty(method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute), true));
    }

    private static IdentityAuthorizationClient Client(HttpClient http) => new(http,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Services:IdentityService"] = "http://identity.test", ["Internal:ServiceApiKey"] = "local-test-key"
        }).Build(), NullLogger<IdentityAuthorizationClient>.Instance);
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }
}
