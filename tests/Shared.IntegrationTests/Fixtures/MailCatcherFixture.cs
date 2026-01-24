using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// MailCatcher test container fixture for integration tests.
/// Provides a real MailCatcher instance running in Docker for email testing.
/// </summary>
public class MailCatcherFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public MailCatcherFixture()
    {
        _container = new ContainerBuilder()
            .WithImage("dockage/mailcatcher:0.8.2")
            .WithPortBinding(1025, true) // SMTP port
            .WithPortBinding(1080, true) // Web UI port
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(r => r
                    .ForPath("/")
                    .ForPort(1080)))
            .WithCleanUp(true)
            .Build();
    }

    /// <summary>
    /// Gets the SMTP host
    /// </summary>
    public string SmtpHost => _container.Hostname;

    /// <summary>
    /// Gets the SMTP port (1025)
    /// </summary>
    public int SmtpPort => _container.GetMappedPublicPort(1025);

    /// <summary>
    /// Gets the Web UI URL
    /// </summary>
    public string WebUrl => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(1080)}";

    /// <summary>
    /// Gets the Web UI port (1080)
    /// </summary>
    public int WebPort => _container.GetMappedPublicPort(1080);

    /// <summary>
    /// Gets the API endpoint for retrieving messages
    /// </summary>
    public string ApiUrl => $"{WebUrl}/messages";

    /// <summary>
    /// Starts the MailCatcher container
    /// </summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    /// <summary>
    /// Stops and disposes the MailCatcher container
    /// </summary>
    public async Task DisposeAsync()
    {
        await _container.StopAsync();
        await _container.DisposeAsync();
    }

    /// <summary>
    /// Clears all messages from MailCatcher
    /// </summary>
    public async Task ClearMessagesAsync()
    {
        using var httpClient = new HttpClient();
        await httpClient.DeleteAsync($"{WebUrl}/messages");
    }

    /// <summary>
    /// Gets all messages from MailCatcher
    /// </summary>
    public async Task<string> GetMessagesAsync()
    {
        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(ApiUrl);
        return await response.Content.ReadAsStringAsync();
    }
}
