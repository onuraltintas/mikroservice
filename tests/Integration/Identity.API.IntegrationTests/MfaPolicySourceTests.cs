using System.Net;
using System.Text;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Identity.API;
using Identity.API.Controllers;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class MfaPolicySourceTests
{
    private const string ServiceKey = "an-internal-service-key-with-32-characters";

    [Fact]
    public async Task IdentityPolicyStore_ShouldReadDatabaseValue()
    {
        await using var context = CreateContext();
        context.Configurations.Add(SystemConfiguration.Create(
            MfaOperationCategories.ConfigurationKey(MfaOperationCategories.Users),
            MfaPolicyModes.Disabled,
            "MFA",
            ConfigurationDataType.String,
            "Security"));
        await context.SaveChangesAsync();
        var store = new DatabaseMfaPolicyStore(context, NullLogger<DatabaseMfaPolicyStore>.Instance);

        var mode = await store.GetModeAsync(MfaOperationCategories.Users);

        mode.Should().Be(MfaPolicyModes.Disabled);
    }

    [Fact]
    public async Task IdentityPolicyStore_ShouldFailClosedWhenPolicyMissing()
    {
        await using var context = CreateContext();
        var store = new DatabaseMfaPolicyStore(context, NullLogger<DatabaseMfaPolicyStore>.Instance);

        var mode = await store.GetModeAsync(MfaOperationCategories.Users);

        mode.Should().Be(MfaPolicyModes.Required);
    }

    [Fact]
    public async Task RemotePolicyStore_ShouldReadIdentityWithoutRedis()
    {
        var handler = new StubHandler(request =>
        {
            request.RequestUri!.AbsolutePath.Should().Be("/api/internal/mfa-policy/users");
            request.Headers.GetValues(InternalServiceAuthentication.HeaderName)
                .Should().ContainSingle().Which.Should().Be(ServiceKey);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"mode\":\"disabled\"}", Encoding.UTF8, "application/json")
            };
        });
        var store = CreateRemoteStore(handler);

        var mode = await store.GetModeAsync(MfaOperationCategories.Users);

        mode.Should().Be(MfaPolicyModes.Disabled);
        handler.RequestCount.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "{\"mode\":\"disabled\"}")]
    [InlineData(HttpStatusCode.OK, "{\"mode\":\"unknown\"}")]
    public async Task RemotePolicyStore_ShouldFailClosedOnUnavailableOrInvalidResponse(
        HttpStatusCode status,
        string body)
    {
        var store = CreateRemoteStore(new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        }));

        var mode = await store.GetModeAsync(MfaOperationCategories.System);

        mode.Should().Be(MfaPolicyModes.Required);
    }

    [Fact]
    public void InternalPolicyEndpoint_ShouldRequireInternalServiceKey()
    {
        var action = typeof(InternalMfaPolicyController).GetMethod(nameof(InternalMfaPolicyController.GetMode));

        action.Should().NotBeNull();
        action!.GetCustomAttributes(typeof(InternalServiceKeyAttribute), inherit: true)
            .Should().ContainSingle();
        action.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true)
            .Should().ContainSingle();
    }

    private static MfaPolicyStore CreateRemoteStore(StubHandler handler)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Services:IdentityService"] = "http://identity-service:8080",
                ["INTERNAL_SERVICE_API_KEY"] = ServiceKey
            }).Build();
        return new MfaPolicyStore(new HttpClient(handler), configuration, NullLogger<MfaPolicyStore>.Instance);
    }

    private static IdentityDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new IdentityDbContext(options);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(response(request));
        }
    }
}
