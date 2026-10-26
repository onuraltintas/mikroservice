namespace EduPlatform.Gateway.UnitTests.Infrastructure;

public sealed class BackendFixture : IAsyncLifetime
{
    public BackendStub Backend { get; private set; } = null!;

    public async Task InitializeAsync() => Backend = await BackendStub.StartAsync();

    public async Task DisposeAsync() => await Backend.DisposeAsync();

    public GatewayFactory CreateGateway(IReadOnlyDictionary<string, string?>? settings = null) =>
        new(Backend.BaseAddress, settings);
}

[CollectionDefinition(Name)]
public sealed class GatewayCollection : ICollectionFixture<BackendFixture>
{
    public const string Name = "Gateway host";
}
