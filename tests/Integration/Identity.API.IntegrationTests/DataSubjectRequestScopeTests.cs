using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;
using Identity.Domain.Enums;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestScopeTests
{
    [Theory]
    [InlineData(PersonalDataScope.Account, "Coaching", "Notification", "SpeedReading")]
    [InlineData(PersonalDataScope.Coaching, "Coaching")]
    [InlineData(PersonalDataScope.SpeedReading, "SpeedReading")]
    public void RequiredServices_ShouldBeIsolatedByProductScope(
        PersonalDataScope scope,
        params string[] expectedServices)
    {
        DataErasureAssessmentScopePolicy.RequiredServices(scope)
            .Should().Equal(expectedServices);
    }

    [Fact]
    public void Create_ShouldPersistExplicitProductScope()
    {
        var request = DataSubjectRequest.Create(
            Guid.NewGuid(),
            DataSubjectRequestType.Erasure,
            PersonalDataScope.SpeedReading,
            "Hızlı okuma verilerimi silin.",
            DateTime.UtcNow);

        request.Scope.Should().Be(PersonalDataScope.SpeedReading);
    }

    [Fact]
    public void Create_ShouldRejectUnknownScope()
    {
        var action = () => DataSubjectRequest.Create(
            Guid.NewGuid(),
            DataSubjectRequestType.Erasure,
            (PersonalDataScope)999,
            "Verilerimi silin.",
            DateTime.UtcNow);

        action.Should().Throw<ArgumentOutOfRangeException>();
    }
}
