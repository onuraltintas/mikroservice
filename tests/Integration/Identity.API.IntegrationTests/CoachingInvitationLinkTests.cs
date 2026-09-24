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
}
