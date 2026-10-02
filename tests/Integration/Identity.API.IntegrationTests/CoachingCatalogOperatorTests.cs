using Coaching.Infrastructure.Catalogs;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCatalogOperatorTests
{
    [Fact]
    public void DefaultsToReadOnlyAndRequiresExplicitWriteConfirmation()
    {
        var options = CatalogOperatorOptions.Parse(["--directory", "catalog", "--source", "approved"]);
        Assert.Equal("check", options.Action);
        Assert.False(options.Apply);
        Assert.Throws<ArgumentException>(() => CatalogOperatorOptions.Parse(["--directory", "catalog", "--source", "approved", "--action", "publish", "--apply"]));
        Assert.Throws<ArgumentException>(() => CatalogOperatorOptions.Parse(["--directory", "catalog", "--source", "approved", "--action", "publish", "--apply", "--database", "coaching"]));
        var confirmed = CatalogOperatorOptions.Parse(["--directory", "catalog", "--source", "approved", "--action", "publish", "--apply", "--database", "coaching", "--publication-authorized"]);
        Assert.True(confirmed.Apply);
        Assert.True(confirmed.PublicationAuthorized);
        Assert.Equal("coaching", confirmed.Database);
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--directory")]
    public void InvalidOrIncompleteOptionsAreRejected(string option) =>
        Assert.Throws<ArgumentException>(() => CatalogOperatorOptions.Parse([option]));

    [Fact]
    public void UnknownActionsAndDuplicateArgumentsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => CatalogOperatorOptions.Parse(["--directory", "catalog", "--source", "approved", "--action", "delete"]));
        Assert.Throws<ArgumentException>(() => CatalogOperatorOptions.Parse(["--directory", "catalog", "--source", "approved", "--source", "other"]));
    }
}
