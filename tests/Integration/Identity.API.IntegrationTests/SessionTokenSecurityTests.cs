using FluentAssertions;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class SessionTokenSecurityTests
{
    [Fact]
    public void RefreshToken_KeepsOnlyTheDigest_AndMatchesTheIssuedValue()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "client-value", DateTime.UtcNow.AddDays(7), "127.0.0.1");

        token.IssuedValue.Should().Be("client-value");
        token.Token.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]+$").And.NotContain("client-value");
        token.Matches("client-value").Should().BeTrue();
        token.Matches(token.Token).Should().BeFalse("the stored digest is not a usable credential");
        token.Matches("other").Should().BeFalse();
        token.Matches(null).Should().BeFalse();
        token.FamilyId.Should().Be(token.Id, "a sign-in starts its own family");
    }

    [Fact]
    public void Rotation_StaysInTheFamily_AndNeverExtendsPastTheAbsoluteLimit()
    {
        var signIn = RefreshToken.Create(
            Guid.NewGuid(), "first", DateTime.UtcNow.AddDays(7), null,
            sessionStartedAt: DateTime.UtcNow.AddDays(-29));
        var successor = RefreshToken.Create(signIn.UserId, "second", DateTime.UtcNow.AddDays(7), null);

        successor.ContinueSessionOf(signIn, TimeSpan.FromDays(30));

        successor.FamilyId.Should().Be(signIn.FamilyId);
        successor.SessionStartedAt.Should().Be(signIn.SessionStartedAt);
        successor.ExpiresAt.Should().BeCloseTo(signIn.SessionStartedAt.AddDays(30), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void PasswordResetToken_IsStoredAsADigest_AndComparedSafely()
    {
        var user = User.Create(Guid.NewGuid(), "reset@example.test");

        var issued = user.GeneratePasswordResetToken();

        user.PasswordResetToken.Should().Be(RefreshToken.Digest(issued)).And.NotBe(issued);
        user.PasswordResetTokenMatches(issued).Should().BeTrue();
        user.PasswordResetTokenMatches(user.PasswordResetToken).Should().BeFalse();
        user.PasswordResetTokenMatches(null).Should().BeFalse();
        user.ClearPasswordResetToken();
        user.PasswordResetTokenMatches(issued).Should().BeFalse("links are single use");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(366)]
    public void SessionLifetime_OutOfRange_IsRejected(int days)
    {
        new SessionLifetimeOptions { MaxSessionDays = days }
            .Invoking(options => options.Validate())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ExpiredOrRevokedTokens_DoNotMatchAsActive()
    {
        var token = RefreshToken.Create(Guid.NewGuid(), "value", DateTime.UtcNow.AddDays(1), null, product: PlatformProduct.Coaching);
        token.Revoke("127.0.0.1", RefreshToken.RotatedReason);

        token.IsActive.Should().BeFalse();
        token.ReasonRevoked.Should().Be(RefreshToken.RotatedReason);
    }
}
