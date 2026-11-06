using FluentAssertions;
using Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Identity.API.IntegrationTests;

/// <summary>
/// E5 (#22): the pending e-mail change is stored on the user row (address, link digest, expiry, request time) by the
/// migration AccountEmailChange; Down removes the columns. Only a confirmed change touches the unique address.
/// </summary>
public sealed class AccountEmailChangePostgresTests : IAsyncLifetime
{
    private const string Previous = "20261105090000_CorrectTurkishLocationNames";
    private const string Migration = "20261106090000_AccountEmailChange";
    private IdentityPostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await IdentityPostgresDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task ThePendingChangeIsStoredAndConfirmedOnPostgres()
    {
        await using var context = _database.CreateContext();
        (await context.Database.GetAppliedMigrationsAsync()).Should().Contain(Migration);
        context.Database.HasPendingModelChanges().Should().BeFalse("the snapshot and the Designer follow the model");

        var user = User.Create(Guid.NewGuid(), "eski@example.test", "Ada", "Kaya");
        var other = User.Create(Guid.NewGuid(), "dolu@example.test", "Can", "Ak");
        context.Users.AddRange(user, other);
        var token = user.RequestEmailChange("dolu@example.test", DateTime.UtcNow);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
        stored.Email.Should().Be("eski@example.test");
        stored.PendingEmail.Should().Be("dolu@example.test", "a pending address may equal another account's: only confirming is checked");
        stored.EmailChangeTokenMatches(token).Should().BeTrue();

        stored.CancelEmailChange();
        var second = stored.RequestEmailChange("yeni@example.test", DateTime.UtcNow);
        stored.EmailChangeTokenMatches(second).Should().BeTrue();
        stored.ConfirmEmailChange().Should().Be("eski@example.test");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        (await context.Users.SingleAsync(u => u.Id == user.Id)).Email.Should().Be("yeni@example.test");
    }

    [Fact]
    public async Task DownRemovesTheColumns()
    {
        await using var context = _database.CreateContext();
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        (await ColumnsAsync(context)).Should().BeEmpty();
        await context.GetService<IMigrator>().MigrateAsync(Migration);
        (await ColumnsAsync(context)).Should().BeEquivalentTo("PendingEmail", "PendingEmailExpiresAt", "PendingEmailRequestedAt", "PendingEmailToken");
    }

    private static async Task<List<string>> ColumnsAsync(DbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'users' AND column_name LIKE 'PendingEmail%'
            """;
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }
}
