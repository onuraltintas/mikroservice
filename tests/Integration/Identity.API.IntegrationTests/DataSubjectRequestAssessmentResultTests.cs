using EduPlatform.Shared.Contracts.Events.Privacy;
using FluentAssertions;
using Identity.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class DataSubjectRequestAssessmentResultTests
{
    [Fact]
    public void Record_ShouldPreserveServiceSpecificInventory()
    {
        var message = new PersonalDataErasureAssessmentCompletedV1(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SpeedReading",
            CanProceed: true, HasActiveLegalHold: false,
            AssignmentCount: 0, AttachmentCount: 0, ExamResultCount: 0,
            GoalCount: 0, SessionCount: 0, AgreementCount: 0,
            DateTime.UtcNow,
            RecordCounts: new Dictionary<string, int>
            {
                ["profiles"] = 1,
                ["exerciseSessions"] = 4,
                ["readingAttempts"] = 3
            });

        var result = DataSubjectRequestAssessmentResult.Record(message);

        result.RecordCounts.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["profiles"] = 1,
            ["exerciseSessions"] = 4,
            ["readingAttempts"] = 3
        });
        result.TotalRecordCount.Should().Be(8);
    }

    [Fact]
    public void Record_ShouldPreserveServiceInventoryAndBlockerState()
    {
        var requestId = Guid.NewGuid();
        var message = new PersonalDataErasureAssessmentCompletedV1(
            Guid.NewGuid(), requestId, Guid.NewGuid(), "Coaching",
            CanProceed: false, HasActiveLegalHold: true,
            AssignmentCount: 2, AttachmentCount: 1, ExamResultCount: 3,
            GoalCount: 4, SessionCount: 5, AgreementCount: 1,
            DateTime.UtcNow);

        var result = DataSubjectRequestAssessmentResult.Record(message);

        result.RequestId.Should().Be(requestId);
        result.ServiceName.Should().Be("Coaching");
        result.CanProceed.Should().BeFalse();
        result.HasActiveLegalHold.Should().BeTrue();
        result.TotalRecordCount.Should().Be(16);
    }
}
