using FluentAssertions;
using Identity.Application.Services;
using Microsoft.Extensions.Configuration;

namespace Identity.API.IntegrationTests;

public sealed class CoachingInvitationLinkTests
{
    [Fact]
    public void Coaching_invitation_targets_coaching_portal_and_not_speed_reading()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicApp:CoachingBaseUrl"] = "https://onuraltintas.net",
            ["PublicApp:BaseUrl"] = "https://masterhizliokuma.com"
        }).Build();
        var invitationId = Guid.NewGuid();

        var link = PublicAppInvitationLink.Create(configuration, invitationId);

        link.Should().Be($"https://onuraltintas.net/coaching-portal/invitations?id={invitationId}");
    }

    [Fact]
    public void Missing_coaching_base_url_must_not_fall_back_to_speed_reading()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["PublicApp:BaseUrl"] = "https://masterhizliokuma.com"
        }).Build();

        Action create = () => PublicAppInvitationLink.Create(configuration, Guid.NewGuid());

        create.Should().Throw<InvalidOperationException>();
    }
}
