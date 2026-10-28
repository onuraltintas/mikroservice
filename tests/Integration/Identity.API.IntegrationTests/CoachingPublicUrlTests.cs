using Coaching.Infrastructure;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

// Roadmap D3: one public site address for coaching links (invitations, newsletter), required in production and staging.
public sealed class CoachingPublicUrlTests
{
    private static IConfiguration Config(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.ToDictionary(x => x.Key, x => (string?)x.Value)).Build();

    [Fact]
    public void LiveEnvironmentsRefuseToStartWithoutAnHttpsAddress()
    {
        Assert.Throws<InvalidOperationException>(() => CoachingPublicUrl.Require(Config(), requireConfigured: true));
        Assert.Throws<InvalidOperationException>(() => CoachingPublicUrl.Require(Config(("Coaching:PublicBaseUrl", "http://eduivme.com")), true));
        Assert.Throws<InvalidOperationException>(() => CoachingPublicUrl.Require(Config(("Coaching:PublicBaseUrl", "eduivme")), true));
        Assert.Equal("https://eduivme.com", CoachingPublicUrl.Require(Config(("Coaching:PublicBaseUrl", "https://eduivme.com/")), true));
    }

    [Fact]
    public void NewsletterAndInvitationsShareTheAddressAndDevelopmentHasADefault()
    {
        Assert.Equal("https://eduivme.com", CoachingPublicUrl.Get(Config()));
        Assert.Equal("http://localhost:4200", CoachingPublicUrl.Get(Config(("Coaching:PublicBaseUrl", "http://localhost:4200"))));
        // The older newsletter key still works when the shared one is missing.
        Assert.Equal("https://ornek.test", CoachingPublicUrl.Get(Config(("CoachingNewsletter:PublicBaseUrl", "https://ornek.test"))));
        Assert.Equal("https://eduivme.com", CoachingPublicUrl.Get(Config(("Coaching:PublicBaseUrl", "https://eduivme.com"), ("CoachingNewsletter:PublicBaseUrl", "https://onuraltintas.net"))));
    }
}
