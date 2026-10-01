using Coaching.Application;
using Coaching.Application.CoachingAgreements;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingCentralLegalConsentWiringTests
{
    [Fact]
    public void CentralRegistrationConsent_ShouldNotBeFollowedByALegacyAgreementGateOnEveryStudentRequest()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.Should().NotContain(descriptor => descriptor.ImplementationType == typeof(CoachingAgreementRequirementBehavior<,>));
    }
}
