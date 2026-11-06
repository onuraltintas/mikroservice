using System.Reflection;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Identity.Application.Commands.ChangeEmail;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Repositories;
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
        (await ColumnsAsync(context)).Should().BeEquivalentTo("PendingEmail", "PendingEmailExpiresAt", "PendingEmailRequestedAt", "PendingEmailToken", "PasswordKnownToOwner");
    }

    // Review M2: Google-created accounts were given a generated password. The migration marks the password of every
    // account with an external sign-in as not chosen by the owner; others keep theirs. A later password sign-in, reset
    // or change marks it known again.
    [Fact]
    public async Task TheMigrationMarksAccountsWithAnExternalSignInAsHavingNoKnownPassword()
    {
        await using var context = _database.CreateContext();
        var google = User.Create(Guid.NewGuid(), "google@example.test", "Ada", "Google");
        var local = User.Create(Guid.NewGuid(), "yerel@example.test", "Can", "Yerel");
        foreach (var user in new[] { google, local })
        {
            new PasswordHasher().CreatePasswordHash("Some-Password-1!", out var hash, out var salt);
            user.SetPassword(hash, salt);
        }
        context.Users.AddRange(google, local);
        await context.SaveChangesAsync();
        await context.GetService<IMigrator>().MigrateAsync(Previous);
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO identity."UserLogins" ("Id", "UserId", "LoginProvider", "ProviderKey", "ProviderDisplayName", "CreatedAt")
            VALUES ({Guid.NewGuid()}, {google.Id}, 'Google', 'google-subject', 'Google', now())
            """);

        await context.GetService<IMigrator>().MigrateAsync(Migration);

        context.ChangeTracker.Clear();
        (await context.Users.SingleAsync(u => u.Id == google.Id)).HasPassword.Should().BeFalse();
        (await context.Users.SingleAsync(u => u.Id == local.Id)).HasPassword.Should().BeTrue();
    }

    // Review L1: the unique address index decides a race the pre-check missed: the answer is Account.EmailInUse.
    [Fact]
    public async Task AnAddressTakenInARaceIsReportedAsInUse()
    {
        var (userId, token) = await PendingChangeAsync("yarisan@example.test");
        await using (var other = _database.CreateContext())
        {
            other.Users.Add(User.Create(Guid.NewGuid(), "yarisan@example.test", "Geç", "Kayıt"));
            await other.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var blind = BlindToAddresses(new UserRepository(context));
        var result = await new ConfirmEmailChangeCommandHandler(blind, new UnitOfWork(context), Publisher())
            .Handle(new ConfirmEmailChangeCommand(userId, token), default);

        result.Error.Code.Should().Be("Account.EmailInUse");
    }

    // Review L2: two confirmations of the same link at the same time: only one succeeds.
    [Fact]
    public async Task TwoConfirmationsAtOnceSucceedOnlyOnce()
    {
        var (userId, token) = await PendingChangeAsync("ikili@example.test");
        await using var first = _database.CreateContext();
        await using var second = _database.CreateContext();
        var firstUser = await new UserRepository(first).GetByIdAsync(userId, default);
        var secondUser = await new UserRepository(second).GetByIdAsync(userId, default);
        firstUser!.EmailChangeTokenMatches(token).Should().BeTrue();
        secondUser!.EmailChangeTokenMatches(token).Should().BeTrue();

        var one = await new ConfirmEmailChangeCommandHandler(new UserRepository(first), new UnitOfWork(first), Publisher())
            .Handle(new ConfirmEmailChangeCommand(userId, token), default);
        var two = await new ConfirmEmailChangeCommandHandler(new UserRepository(second), new UnitOfWork(second), Publisher())
            .Handle(new ConfirmEmailChangeCommand(userId, token), default);

        one.IsSuccess.Should().BeTrue();
        two.Error.Code.Should().Be("EmailChange.InvalidLink");
    }

    private async Task<(Guid UserId, string Token)> PendingChangeAsync(string newEmail)
    {
        await using var context = _database.CreateContext();
        var user = User.Create(Guid.NewGuid(), $"{Guid.NewGuid():N}@example.test", "Ada", "Kaya");
        var token = user.RequestEmailChange(newEmail, DateTime.UtcNow);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return (user.Id, token);
    }

    private static MassTransit.IPublishEndpoint Publisher()
    {
        var proxy = DispatchProxy.Create<MassTransit.IPublishEndpoint, AccountEmailChangeTests.PublishProxy>();
        ((AccountEmailChangeTests.PublishProxy)(object)proxy).Published = [];
        return proxy;
    }

    /// <summary>The real repository, except that the address pre-check finds nobody (as in a race).</summary>
    private static IUserRepository BlindToAddresses(IUserRepository inner)
    {
        var proxy = DispatchProxy.Create<IUserRepository, BlindProxy>();
        ((BlindProxy)(object)proxy).Inner = inner;
        return proxy;
    }

    public class BlindProxy : DispatchProxy
    {
        public IUserRepository Inner = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) =>
            method!.Name == nameof(IUserRepository.GetByEmailAsync)
                ? Task.FromResult<User?>(null)
                : method.Invoke(Inner, args);
    }

    private static async Task<List<string>> ColumnsAsync(DbContext context)
    {
        var connection = context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name FROM information_schema.columns
            WHERE table_schema = 'identity' AND table_name = 'users' AND (column_name LIKE 'PendingEmail%' OR column_name = 'PasswordKnownToOwner')
            """;
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) names.Add(reader.GetString(0));
        return names;
    }
}
