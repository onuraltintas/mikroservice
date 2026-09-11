using FluentAssertions;
using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.UnitTests;

public sealed class AssessmentStudyEnrollmentTests
{
    [Fact]
    public void Enrollment_pins_protocol_and_cohort_and_can_be_withdrawn()
    {
        var now = DateTime.UtcNow;
        var enrollment = AssessmentStudyEnrollment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "pilot-2026", "protocol-v1", "treatment",
            consentRecordedAt: now.AddMinutes(-1), Guid.NewGuid(), now);

        enrollment.StudyCode.Should().Be("pilot-2026");
        enrollment.ProtocolVersion.Should().Be("protocol-v1");
        enrollment.CohortCode.Should().Be("treatment");
        enrollment.IsActive.Should().BeTrue();

        enrollment.Withdraw(Guid.NewGuid(), now.AddDays(1));
        enrollment.IsActive.Should().BeFalse();
        enrollment.WithdrawnAt.Should().Be(now.AddDays(1));
    }

    [Theory]
    [InlineData("", "v1", "control")]
    [InlineData("study", "", "control")]
    [InlineData("study", "v1", "")]
    public void Enrollment_rejects_missing_research_metadata(string study, string protocol, string cohort)
    {
        var act = () => AssessmentStudyEnrollment.Create(
            Guid.NewGuid(), Guid.NewGuid(), study, protocol, cohort,
            DateTime.UtcNow, Guid.NewGuid(), DateTime.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Enrollment_rejects_consent_recorded_after_enrollment()
    {
        var enrolledAt = DateTime.UtcNow;
        var act = () => AssessmentStudyEnrollment.Create(
            Guid.NewGuid(), Guid.NewGuid(), "study", "v1", "control",
            enrolledAt.AddSeconds(1), Guid.NewGuid(), enrolledAt);

        act.Should().Throw<ArgumentException>();
    }
}
