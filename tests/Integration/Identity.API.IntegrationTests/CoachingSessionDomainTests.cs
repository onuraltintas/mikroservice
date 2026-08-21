using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingSessionDomainTests
{
    [Fact]
    public void NewSessionParticipant_ShouldNotBeMarkedPresentBeforeAttendanceIsRecorded()
    {
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Matematik koçluğu",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne);

        session.AddStudent(Guid.NewGuid());

        session.Attendances.Should().ContainSingle()
            .Which.AttendanceStatus.Should().Be(AttendanceStatus.NotRecorded);
    }

    [Fact]
    public void GroupSession_ShouldKeepEveryParticipant()
    {
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Grup koçluğu",
            DateTime.UtcNow.AddDays(1),
            SessionType.Group);
        var studentIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        session.AddStudents(studentIds);

        session.Attendances.Select(item => item.StudentId)
            .Should().BeEquivalentTo(studentIds);
    }
}
