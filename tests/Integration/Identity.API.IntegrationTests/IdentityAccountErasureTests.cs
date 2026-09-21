using FluentAssertions;
using Identity.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class IdentityAccountErasureTests
{
    [Fact]
    public void AnonymizeForErasure_ShouldRemoveCredentialsAndPersonalIdentifiers()
    {
        var id = Guid.NewGuid();
        var user = User.Create(id, "person@example.com", "Kişi", "Adı");
        user.SetPassword([1, 2], [3, 4]);
        user.SetPhoneNumber("+905551112233", true);
        user.GenerateEmailVerificationToken();
        user.GeneratePasswordResetToken();
        user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);

        user.AnonymizeForErasure();

        user.Email.Should().Be($"erased-{id:N}@deleted.invalid");
        user.FirstName.Should().BeEmpty();
        user.LastName.Should().BeEmpty();
        user.PhoneNumber.Should().BeNull();
        user.PasswordHash.Should().BeEmpty();
        user.PasswordSalt.Should().BeEmpty();
        user.EmailVerificationToken.Should().BeNull();
        user.PasswordResetToken.Should().BeNull();
        user.MfaEnabled.Should().BeFalse();
        user.MfaSecretProtected.Should().BeNull();
        user.IsActive.Should().BeFalse();
    }
}
