using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Identity.API.IntegrationTests;

/// <summary>
/// A migrated, throw-away Identity database on real PostgreSQL. Uses IDENTITY_TEST_POSTGRES
/// when set (any reachable server), otherwise starts a container.
/// </summary>
public sealed class IdentityPostgresDatabase : IAsyncDisposable
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public static async Task<IdentityPostgresDatabase> CreateAsync()
    {
        var database = new IdentityPostgresDatabase();
        var external = Environment.GetEnvironmentVariable("IDENTITY_TEST_POSTGRES");
        string server;
        if (!string.IsNullOrWhiteSpace(external))
        {
            server = external;
        }
        else
        {
            database._container = new PostgreSqlBuilder("postgres:16-alpine").WithCleanUp(true).Build();
            await database._container.StartAsync();
            server = database._container.GetConnectionString();
        }

        database.ConnectionString = new NpgsqlConnectionStringBuilder(server)
        {
            Database = $"identity_test_{Guid.NewGuid():N}"
        }.ConnectionString;
        await using var context = database.CreateContext();
        await context.Database.MigrateAsync();
        return database;
    }

    public IdentityDbContext CreateContext() => new(
        new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask DisposeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

/// <summary>
/// Refresh-token storage against real PostgreSQL with the real migrations: digests at
/// rest, the unique lookup index, family revocation on reuse and the retention purge.
/// </summary>
public sealed class RefreshTokenStoragePostgresTests : IAsyncLifetime
{
    private IdentityPostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await IdentityPostgresDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task IssuedValue_IsStoredOnlyAsADigest_AndStillResolvesTheSession()
    {
        var user = await SeedUserAsync();
        var token = await IssueAsync(user.Id, "issued-value-1");

        await using var context = CreateContext();
        var stored = await context.RefreshTokens.AsNoTracking().SingleAsync(row => row.Id == token.Id);
        stored.Token.Should().Be(RefreshToken.Digest("issued-value-1")).And.NotContain("issued-value-1");
        stored.IssuedValue.Should().BeNull("the value is never persisted");

        var resolved = await new UserRepository(context).GetByRefreshTokenAsync("issued-value-1", CancellationToken.None);
        resolved.Should().NotBeNull();
        resolved!.RefreshTokens.Should().ContainSingle(candidate => candidate.Matches("issued-value-1"));
        (await new UserRepository(context).GetByRefreshTokenAsync(stored.Token, CancellationToken.None))
            .Should().BeNull("presenting the stored digest must not work");
    }

    [Fact]
    public async Task ReusingARotatedToken_RevokesTheWholeFamily()
    {
        var user = await SeedUserAsync();
        var first = await IssueAsync(user.Id, "family-first");
        var successor = RefreshToken.Create(user.Id, "family-second", DateTime.UtcNow.AddDays(7), null, product: PlatformProduct.Coaching);
        successor.ContinueSessionOf(first, TimeSpan.FromDays(30));

        await using (var context = CreateContext())
        {
            (await new UserRepository(context).RotateRefreshTokenAsync(
                "family-first", successor, "127.0.0.1", RefreshToken.RotatedReason, CancellationToken.None)).Should().BeTrue();
        }

        await using (var context = CreateContext())
        {
            var repository = new UserRepository(context);
            (await repository.RevokeRefreshTokenFamilyOnReuseAsync("unknown-value", CancellationToken.None)).Should().BeFalse();
            (await repository.RevokeRefreshTokenFamilyOnReuseAsync("family-first", CancellationToken.None)).Should().BeTrue();
        }

        await using (var context = CreateContext())
        {
            var family = await context.RefreshTokens.AsNoTracking().Where(row => row.FamilyId == first.FamilyId).ToListAsync();
            family.Should().HaveCount(2).And.OnlyContain(row => row.RevokedAt != null);
            family.Single(row => row.Id == successor.Id).ReasonRevoked.Should().Be("refresh token reuse detected");
        }
    }

    [Fact]
    public async Task DuplicateDigest_IsRejectedByTheUniqueIndex()
    {
        var user = await SeedUserAsync();
        await IssueAsync(user.Id, "same-value");

        var duplicate = () => IssueAsync(user.Id, "same-value");

        await duplicate.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Purge_DeletesOnlyTokensExpiredOrRevokedBeforeTheCutoff()
    {
        var user = await SeedUserAsync();
        var active = await IssueAsync(user.Id, "purge-active");
        var oldExpired = await IssueAsync(user.Id, "purge-expired", DateTime.UtcNow.AddDays(-20));
        var oldRevoked = await IssueAsync(user.Id, "purge-revoked");
        var recentRevoked = await IssueAsync(user.Id, "purge-recent");
        await using (var context = CreateContext())
        {
            await context.RefreshTokens.Where(row => row.Id == oldRevoked.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.RevokedAt, DateTime.UtcNow.AddDays(-20)));
            await context.RefreshTokens.Where(row => row.Id == recentRevoked.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.RevokedAt, DateTime.UtcNow.AddDays(-1)));
        }

        await using (var context = CreateContext())
        {
            var deleted = await new UserRepository(context).PurgeRefreshTokensAsync(
                DateTime.UtcNow.AddDays(-14), batchSize: 1, CancellationToken.None);
            deleted.Should().Be(1, "one batch is bounded by the batch size");
            deleted += await new UserRepository(context).PurgeRefreshTokensAsync(
                DateTime.UtcNow.AddDays(-14), batchSize: 100, CancellationToken.None);
            deleted.Should().Be(2);
        }

        await using (var context = CreateContext())
        {
            var remaining = await context.RefreshTokens.AsNoTracking().Select(row => row.Id).ToListAsync();
            remaining.Should().BeEquivalentTo([active.Id, recentRevoked.Id]);
            remaining.Should().NotContain([oldExpired.Id, oldRevoked.Id]);
        }
    }

    private IdentityDbContext CreateContext() => _database.CreateContext();

    private async Task<User> SeedUserAsync()
    {
        var user = User.Create(Guid.NewGuid(), $"tokens-{Guid.NewGuid():N}@example.test");
        user.SetPassword(new byte[64], new byte[32]);
        await using var context = CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private async Task<RefreshToken> IssueAsync(Guid userId, string value, DateTime? expiresAt = null)
    {
        var token = RefreshToken.Create(userId, value, expiresAt ?? DateTime.UtcNow.AddDays(7), "127.0.0.1", product: PlatformProduct.Coaching);
        await using var context = CreateContext();
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        return token;
    }
}
