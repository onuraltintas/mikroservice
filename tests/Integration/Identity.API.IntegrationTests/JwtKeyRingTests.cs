using EduPlatform.Shared.Security.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public class JwtKeyRingTests
{
    private const string ActiveSecret = "active-secret-with-at-least-32-random-bytes";
    private const string PreviousSecret = "previous-secret-with-at-least-32-random-bytes";

    [Fact]
    public void FromConfiguration_ShouldExposeActiveAndPreviousKeysForOverlap()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = ActiveSecret,
            ["JWT_PREVIOUS_SECRETS"] = $" {PreviousSecret} ",
            ["JWT_KEY_ID"] = "2026-08",
            ["JWT_PREVIOUS_KEY_IDS"] = "2026-07"
        });

        var keyRing = JwtKeyRing.FromConfiguration(configuration);

        keyRing.ActiveSecret.Should().Be(ActiveSecret);
        keyRing.ActiveKeyId.Should().Be("2026-08");
        keyRing.ValidationKeys.Select(key => key.Secret).Should()
            .Equal(ActiveSecret, PreviousSecret);
        keyRing.ValidationKeys.Select(key => key.KeyId).Should()
            .Equal("2026-08", "2026-07");
    }

    [Fact]
    public void ProductionPlaceholderSecret_ShouldFailClosed()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["JWT_SECRET"] = "replace-with-at-least-32-random-characters"
        });

        var action = () => JwtKeyRing.FromConfiguration(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*placeholder*Production*");
    }

    [Fact]
    public void ProductionShortPreviousSecret_ShouldFailClosed()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = "Production",
            ["JWT_SECRET"] = ActiveSecret,
            ["JWT_PREVIOUS_SECRETS"] = "too-short"
        });

        var action = () => JwtKeyRing.FromConfiguration(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*at least 32 UTF-8 bytes*");
    }

    [Fact]
    public void EmptyAspNetEnvironment_ShouldNotOverrideProductionEnvironment()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ASPNETCORE_ENVIRONMENT"] = string.Empty,
            ["ENVIRONMENT"] = "Production",
            ["JWT_SECRET"] = "replace-with-at-least-32-random-characters"
        });

        var action = () => JwtKeyRing.FromConfiguration(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*placeholder*Production*");
    }

    [Fact]
    public void MismatchedPreviousKeyIds_ShouldBeRejected()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = ActiveSecret,
            ["JWT_PREVIOUS_SECRETS"] = PreviousSecret,
            ["JWT_PREVIOUS_KEY_IDS"] = "old-1,old-2"
        });

        var action = () => JwtKeyRing.FromConfiguration(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT_PREVIOUS_KEY_IDS*same number*");
    }

    [Fact]
    public void ActiveKeyId_WithPreviousSecretsWithoutPreviousIds_ShouldBeRejected()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["JWT_SECRET"] = ActiveSecret,
            ["JWT_KEY_ID"] = "2026-08",
            ["JWT_PREVIOUS_SECRETS"] = PreviousSecret
        });

        var action = () => JwtKeyRing.FromConfiguration(configuration);

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT_PREVIOUS_KEY_IDS*previous secret*JWT_KEY_ID*");
    }

    private static IConfiguration CreateConfiguration(IReadOnlyDictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
