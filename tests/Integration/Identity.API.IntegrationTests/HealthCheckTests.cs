using FluentAssertions;
using Shared.IntegrationTests.Fixtures;
using System.Net;
using Xunit;
using Xunit.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Integration tests for basic connectivity
/// These tests verify that the test infrastructure is working correctly
/// </summary>
[Collection("Database")]
public class HealthCheckTests
{
    private readonly PostgresFixture _postgresFixture;
    private readonly ITestOutputHelper _output;

    public HealthCheckTests(PostgresFixture postgresFixture, ITestOutputHelper output)
    {
        _postgresFixture = postgresFixture;
        _output = output;
    }

    [Fact]
    public async Task PostgresContainer_ShouldBeAccessible()
    {
        // Arrange
        _output.WriteLine($"PostgreSQL Connection String: {_postgresFixture.ConnectionString}");
        _output.WriteLine($"PostgreSQL Host: {_postgresFixture.Host}");
        _output.WriteLine($"PostgreSQL Port: {_postgresFixture.Port}");

        // Act & Assert
        _postgresFixture.ConnectionString.Should().NotBeNullOrEmpty("connection string should be available");
        _postgresFixture.Host.Should().NotBeNullOrEmpty("host should be available");
        _postgresFixture.Port.Should().BeGreaterThan(0, "port should be assigned");
        
        // Verify connection string format
        _postgresFixture.ConnectionString.Should().Contain("Host=", "connection string should contain host");
        _postgresFixture.ConnectionString.Should().Contain("Database=", "connection string should contain database");
    }

    [Fact]
    public async Task RunningApiGateway_ShouldBeHealthy()
    {
        // Docker Compose exposes only the API Gateway. Service ports remain internal.
        
        // Arrange
        using var httpClient = new HttpClient();
        var gatewayUrl = "http://localhost:5000";

        try
        {
            // Act
            var response = await httpClient.GetAsync($"{gatewayUrl}/health");
            var content = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Health Check Response: {response.StatusCode}");
            _output.WriteLine($"Health Check Content: {content}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, "health endpoint should return OK");
            content.Should().Contain("Healthy", "health check should report healthy status");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"Note: API Gateway not running on {gatewayUrl}");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine("This test requires the API Gateway to be running (docker compose up)");
            
            // Skip test if service is not running
            throw new SkipException($"API Gateway is not running on {gatewayUrl}. Start with 'docker compose up' to run this test.");
        }
    }

    [Fact]
    public async Task RunningApiGateway_ShouldExposeGatewayEndpoint()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var gatewayUrl = "http://localhost:5000";

        try
        {
            var response = await httpClient.GetAsync($"{gatewayUrl}/health");

            _output.WriteLine($"Swagger Response: {response.StatusCode}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, "gateway health endpoint should be accessible");
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("Healthy", "gateway health endpoint should report healthy status");
        }
        catch (HttpRequestException)
        {
            _output.WriteLine($"Note: API Gateway not running on {gatewayUrl}");
            throw new SkipException($"API Gateway is not running. Start with 'docker compose up' to run this test.");
        }
    }
}

/// <summary>
/// Custom exception to skip tests when dependencies are not available
/// </summary>
public class SkipException : Exception
{
    public SkipException(string message) : base(message) { }
}
