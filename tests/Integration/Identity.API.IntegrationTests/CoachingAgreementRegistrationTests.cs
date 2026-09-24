using Coaching.Application;
using Coaching.Application.CoachingAgreements;
using Microsoft.Extensions.DependencyInjection;

namespace Identity.API.IntegrationTests;

public sealed class CoachingAgreementRegistrationTests
{
    [Fact]
    public void StudentQueries_ShouldNotBeGatedByAnAgreement()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.DoesNotContain(services, service =>
            service.ImplementationType == typeof(CoachingAgreementRequirementBehavior<,>));
    }
}
