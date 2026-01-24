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
    public async Task RunningIdentityService_ShouldBeHealthy()
    {
        // This test verifies the running Identity Service (from docker-compose)
        // Not using WebApplicationFactory due to configuration complexity
        
        // Arrange
        using var httpClient = new HttpClient();
        var identityServiceUrl = "http://localhost:5001";

        try
        {
            // Act
            var response = await httpClient.GetAsync($"{identityServiceUrl}/health");
            var content = await response.Content.ReadAsStringAsync();

            _output.WriteLine($"Health Check Response: {response.StatusCode}");
            _output.WriteLine($"Health Check Content: {content}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, "health endpoint should return OK");
            content.Should().Contain("Healthy", "health check should report healthy status");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"Note: Identity Service not running on {identityServiceUrl}");
            _output.WriteLine($"Error: {ex.Message}");
            _output.WriteLine("This test requires the Identity Service to be running (docker-compose up)");
            
            // Skip test if service is not running
            throw new SkipException($"Identity Service is not running on {identityServiceUrl}. Start with 'docker-compose up' to run this test.");
        }
    }

    [Fact]
    public async Task RunningIdentityService_SwaggerShouldBeAccessible()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var identityServiceUrl = "http://localhost:5001";

        try
        {
            // Act - Test Swagger JSON endpoint
            var response = await httpClient.GetAsync($"{identityServiceUrl}/swagger/v1/swagger.json");

            _output.WriteLine($"Swagger Response: {response.StatusCode}");

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, "swagger JSON should be accessible");
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("openapi", "response should be a valid OpenAPI document");
        }
        catch (HttpRequestException ex)
        {
            _output.WriteLine($"Note: Identity Service not running on {identityServiceUrl}");
            throw new SkipException($"Identity Service is not running. Start with 'docker-compose up' to run this test.");
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
