using Xunit;

namespace Shared.IntegrationTests.Fixtures;

/// <summary>
/// Collection definition for PostgreSQL integration tests.
/// All tests in this collection will share the same PostgreSQL container instance.
/// </summary>
[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
