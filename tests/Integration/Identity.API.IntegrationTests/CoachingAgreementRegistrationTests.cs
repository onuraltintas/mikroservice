using Coaching.Application;
using MediatR;
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
            service.ServiceType.IsGenericType
            && service.ServiceType.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));
    }
}
