using Shared.IntegrationTests.Fixtures;
using Xunit;

namespace Identity.API.IntegrationTests;

// Collection definitions must live in the test assembly so xUnit can discover
// the fixtures supplied by the shared integration-test project.
[CollectionDefinition("Database")]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
}

[CollectionDefinition("Cache")]
public sealed class CacheCollection : ICollectionFixture<RedisFixture>
{
}

[CollectionDefinition("MessageBus")]
public sealed class MessageBusCollection : ICollectionFixture<RabbitMqFixture>
{
}

[CollectionDefinition("Email")]
public sealed class EmailCollection : ICollectionFixture<MailCatcherFixture>
{
}
