using FluentAssertions;
using Identity.Domain.Entities;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class EmailNormalizationTests
{
    [Theory]
    [InlineData("Ada.Lovelace@Example.COM", "ada.lovelace@example.com")]
    [InlineData("  padded@example.test  ", "padded@example.test")]
    [InlineData("already@lower.test", "already@lower.test")]
    public void NewAccounts_StoreTheCanonicalAddress(string entered, string stored)
    {
        User.Create(Guid.NewGuid(), entered).Email.Should().Be(stored);
        User.NormalizeEmail(entered).Should().Be(stored);
    }
}

/// <summary>Case-insensitive identity on real PostgreSQL with the real migrations.</summary>
public sealed class EmailNormalizationPostgresTests : IAsyncLifetime
{
    private IdentityPostgresDatabase _database = null!;

    public async Task InitializeAsync() => _database = await IdentityPostgresDatabase.CreateAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    [Fact]
    public async Task Lookup_IgnoresLetterCaseAndSurroundingSpace()
    {
        var user = await SeedAsync("Grace.Hopper@Example.org");

        await using var context = _database.CreateContext();
        var found = await new UserRepository(context).GetByEmailAsync("  GRACE.hopper@example.ORG ", CancellationToken.None);

        found.Should().NotBeNull();
        found!.Id.Should().Be(user.Id);
    }

    [Fact]
    public async Task SecondAccountDifferingOnlyByCase_IsRejectedByTheUniqueIndex()
    {
        await SeedAsync("race@example.test");

        var duplicate = async () => await SeedAsync("RACE@Example.test");

        var failure = await duplicate.Should().ThrowAsync<DbUpdateException>();
        UserRepository.IsDuplicateEmailViolation(failure.Which).Should().BeTrue(
            "registration maps this violation to Identity.UserExists");
    }

    private async Task<User> SeedAsync(string email)
    {
        var user = User.Create(Guid.NewGuid(), email);
        user.SetPassword(new byte[64], new byte[32]);
        await using var context = _database.CreateContext();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
