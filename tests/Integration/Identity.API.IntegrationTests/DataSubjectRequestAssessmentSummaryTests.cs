using FluentAssertions;
using Identity.Application.DataSubjectRequests;
using Identity.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestAssessmentSummaryTests
{
    [Fact]
    public void Create_ShouldReportMissingRequiredServices()
    {
        var summary = DataSubjectRequestAssessmentSummaryDto.Create(
            ["Coaching", "Notification"],
            [Result("Coaching", canProceed: true, hasHold: false)]);

        summary.IsComplete.Should().BeFalse();
        summary.IsReadyForErasure.Should().BeFalse();
        summary.MissingServices.Should().Equal("Notification");
    }

    [Fact]
    public void Create_ShouldBlockWhenAnyServiceHasLegalHold()
    {
        var summary = DataSubjectRequestAssessmentSummaryDto.Create(
            ["Coaching"],
            [Result("Coaching", canProceed: false, hasHold: true)]);

        summary.IsComplete.Should().BeTrue();
        summary.HasBlockingLegalHold.Should().BeTrue();
        summary.IsReadyForErasure.Should().BeFalse();
    }

    [Fact]
    public void Create_ShouldBeReadyOnlyWhenAllRequiredServicesAllowProceeding()
    {
        var summary = DataSubjectRequestAssessmentSummaryDto.Create(
            ["Coaching"],
            [Result("Coaching", canProceed: true, hasHold: false)]);

        summary.IsReadyForErasure.Should().BeTrue();
        summary.TotalRecordCount.Should().Be(6);
    }

    private static DataSubjectRequestAssessmentResult Result(
        string serviceName,
        bool canProceed,
        bool hasHold)
    {
        var message = new EduPlatform.Shared.Contracts.Events.Privacy.PersonalDataErasureAssessmentCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), serviceName,
            canProceed, hasHold, 1, 1, 1, 1, 1, 1, DateTime.UtcNow);
        return DataSubjectRequestAssessmentResult.Record(message);
    }
}
