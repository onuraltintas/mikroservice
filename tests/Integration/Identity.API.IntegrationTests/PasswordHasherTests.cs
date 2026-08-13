using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Xunit;

namespace Identity.API.IntegrationTests;

public class PasswordHasherTests
{
    [Fact]
    public void CreatePasswordHash_ShouldUseSlowPasswordDerivationParameters()
    {
        var hasher = new PasswordHasher();

        hasher.CreatePasswordHash("Correct-Horse-1!", out var hash, out var salt);

        hash.Should().HaveCount(32);
        salt.Should().HaveCount(32);
        hasher.VerifyPasswordHash("Correct-Horse-1!", hash, salt).Should().BeTrue();
        hasher.VerifyPasswordHash("wrong", hash, salt).Should().BeFalse();
        hasher.NeedsRehash(hash, salt).Should().BeFalse();
    }

    [Fact]
    public void LegacyHash_ShouldVerifyButRequireRehash()
    {
        var hasher = new PasswordHasher();
        using var legacyHmac = new System.Security.Cryptography.HMACSHA512();
        var salt = legacyHmac.Key;
        var hash = legacyHmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("Correct-Horse-1!"));

        hasher.VerifyPasswordHash("Correct-Horse-1!", hash, salt).Should().BeTrue();
        hasher.NeedsRehash(hash, salt).Should().BeTrue();
    }
}
