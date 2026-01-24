using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for RabbitMQ integration tests.
/// All tests in this collection will share the same RabbitMQ container instance.
/// </summary>
[CollectionDefinition("MessageBus")]
public class MessageBusCollection : ICollectionFixture<RabbitMqFixture>
{
}

/// <summary>
/// Collection definition for Redis integration tests.
/// All tests in this collection will share the same Redis container instance.
/// </summary>
[CollectionDefinition("Cache")]
public class CacheCollection : ICollectionFixture<RedisFixture>
{
}

/// <summary>
/// Collection definition for Keycloak integration tests.
/// All tests in this collection will share the same Keycloak container instance.
/// </summary>
[CollectionDefinition("Authentication")]
public class AuthenticationCollection : ICollectionFixture<KeycloakFixture>
{
}

/// <summary>
/// Collection definition for MailCatcher integration tests.
/// All tests in this collection will share the same MailCatcher container instance.
/// </summary>
[CollectionDefinition("Email")]
public class EmailCollection : ICollectionFixture<MailCatcherFixture>
{
}
