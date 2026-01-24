using FluentAssertions;
using Shared.IntegrationTests.Fixtures;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Integration tests for Keycloak authentication
/// Tests OAuth2/OpenID Connect flows
/// </summary>
[Collection("Authentication")]
public class KeycloakAuthenticationTests : IAsyncLifetime
{
    private readonly KeycloakFixture _keycloakFixture;
    private readonly ITestOutputHelper _output;
    private HttpClient? _httpClient;
    private string? _adminToken;

    public KeycloakAuthenticationTests(KeycloakFixture keycloakFixture, ITestOutputHelper output)
    {
        _keycloakFixture = keycloakFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"Keycloak Base URL: {_keycloakFixture.BaseUrl}");
        _output.WriteLine($"Keycloak Admin: {_keycloakFixture.AdminUsername}");

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_keycloakFixture.BaseUrl)
        };

        // Get admin token for setup
        _adminToken = await GetAdminTokenAsync();
        _output.WriteLine("Admin token obtained");

        // Create test realm if it doesn't exist
        await EnsureTestRealmExistsAsync();
    }

    public Task DisposeAsync()
    {
        _httpClient?.Dispose();
        return Task.CompletedTask;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = _keycloakFixture.AdminUsername,
            ["password"] = _keycloakFixture.AdminPassword
        });

        var response = await _httpClient!.PostAsync("/realms/master/protocol/openid-connect/token", tokenRequest);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<JsonElement>();
        return tokenResponse.GetProperty("access_token").GetString()!;
    }

    private async Task EnsureTestRealmExistsAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/realms/test-realm");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        var response = await _httpClient!.SendAsync(request);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // Create test realm
            var createRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/realms");
            createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
            createRequest.Content = JsonContent.Create(new
            {
                realm = "test-realm",
                enabled = true,
                displayName = "Test Realm"
            });

            var createResponse = await _httpClient.SendAsync(createRequest);
            createResponse.EnsureSuccessStatusCode();
            _output.WriteLine("Test realm created");
        }
        else
        {
            _output.WriteLine("Test realm already exists");
        }
    }

    [Fact]
    public async Task Keycloak_ShouldBeAccessible()
    {
        // Act
        var response = await _httpClient!.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Keycloak should be accessible");
    }

    [Fact]
    public async Task GetWellKnownConfiguration_ShouldReturnOpenIdConfig()
    {
        // Act
        var response = await _httpClient!.GetAsync("/realms/master/.well-known/openid-configuration");
        var content = await response.Content.ReadAsStringAsync();

        _output.WriteLine($"OpenID Configuration: {content.Substring(0, Math.Min(200, content.Length))}...");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var config = await response.Content.ReadFromJsonAsync<JsonElement>();
        config.GetProperty("issuer").GetString().Should().Contain("/realms/master");
        config.GetProperty("authorization_endpoint").GetString().Should().NotBeNullOrEmpty();
        config.GetProperty("token_endpoint").GetString().Should().NotBeNullOrEmpty();
        config.GetProperty("userinfo_endpoint").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AdminLogin_WithValidCredentials_ShouldReturnToken()
    {
        // Arrange
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = _keycloakFixture.AdminUsername,
            ["password"] = _keycloakFixture.AdminPassword
        });

        // Act
        var response = await _httpClient!.PostAsync("/realms/master/protocol/openid-connect/token", tokenRequest);
        var tokenResponse = await response.Content.ReadFromJsonAsync<JsonElement>();

        _output.WriteLine($"Token Response: {tokenResponse}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        tokenResponse.GetProperty("access_token").GetString().Should().NotBeNullOrEmpty();
        tokenResponse.GetProperty("token_type").GetString().Should().Be("Bearer");
        tokenResponse.GetProperty("expires_in").GetInt32().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task AdminLogin_WithInvalidCredentials_ShouldFail()
    {
        // Arrange
        var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["client_id"] = "admin-cli",
            ["username"] = "invalid",
            ["password"] = "wrong"
        });

        // Act
        var response = await _httpClient!.PostAsync("/realms/master/protocol/openid-connect/token", tokenRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "invalid credentials should be rejected");
    }

    [Fact]
    public async Task GetAdminRealms_WithValidToken_ShouldSucceed()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/realms");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);

        // Act
        var response = await _httpClient!.SendAsync(request);
        var realms = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        _output.WriteLine($"Found {realms?.Length ?? 0} realms");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        realms.Should().NotBeNull();
        realms.Should().Contain(r => r.GetProperty("realm").GetString() == "master");
    }

    [Fact]
    public async Task GetAdminRealms_WithoutToken_ShouldFail()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/admin/realms");
        // No authorization header

        // Act
        var response = await _httpClient!.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "requests without token should be rejected");
    }

    [Fact]
    public async Task CreateAndDeleteClient_ShouldWork()
    {
        // Arrange
        var clientId = $"test-client-{Guid.NewGuid():N}";
        var createRequest = new HttpRequestMessage(HttpMethod.Post, "/admin/realms/test-realm/clients");
        createRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        createRequest.Content = JsonContent.Create(new
        {
            clientId = clientId,
            enabled = true,
            publicClient = false,
            serviceAccountsEnabled = true,
            directAccessGrantsEnabled = true
        });

        // Act - Create
        var createResponse = await _httpClient!.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();

        _output.WriteLine($"Client {clientId} created");

        // Get client to verify
        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/admin/realms/test-realm/clients?clientId={clientId}");
        getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        var getResponse = await _httpClient.SendAsync(getRequest);
        var clients = await getResponse.Content.ReadFromJsonAsync<JsonElement[]>();

        // Assert
        clients.Should().NotBeNull();
        clients.Should().HaveCountGreaterThan(0);
        clients![0].GetProperty("clientId").GetString().Should().Be(clientId);

        // Cleanup - Delete
        var clientUuid = clients[0].GetProperty("id").GetString();
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/admin/realms/test-realm/clients/{clientUuid}");
        deleteRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        var deleteResponse = await _httpClient.SendAsync(deleteRequest);
        deleteResponse.EnsureSuccessStatusCode();

        _output.WriteLine($"Client {clientId} deleted");
    }

    [Fact]
    public async Task TokenIntrospection_ShouldValidateToken()
    {
        // Arrange - Get a valid token first
        var token = await GetAdminTokenAsync();

        var introspectRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = token,
            ["client_id"] = "admin-cli"
        });

        // Act
        var response = await _httpClient!.PostAsync("/realms/master/protocol/openid-connect/token/introspect", introspectRequest);
        var introspection = await response.Content.ReadFromJsonAsync<JsonElement>();

        _output.WriteLine($"Introspection: {introspection}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        introspection.GetProperty("active").GetBoolean().Should().BeTrue("token should be active");
        introspection.GetProperty("client_id").GetString().Should().Be("admin-cli");
    }

    [Fact]
    public async Task GetRealmPublicKey_ShouldReturnJWKS()
    {
        // Act
        var response = await _httpClient!.GetAsync("/realms/master/protocol/openid-connect/certs");
        var jwks = await response.Content.ReadFromJsonAsync<JsonElement>();

        _output.WriteLine($"JWKS: {jwks}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        jwks.GetProperty("keys").GetArrayLength().Should().BeGreaterThan(0, "should have at least one key");
        
        var firstKey = jwks.GetProperty("keys")[0];
        firstKey.GetProperty("kty").GetString().Should().NotBeNullOrEmpty("key type should be present");
        firstKey.GetProperty("use").GetString().Should().Be("sig", "key should be for signature");
    }
}
