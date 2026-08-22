using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
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

    [Fact]
    public void CompletedSession_ShouldRejectRescheduling()
    {
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Tamamlanan seans",
            DateTime.UtcNow.AddDays(1),
            SessionType.OneOnOne);
        session.Complete();

        var action = () => session.UpdateEditableDetails(
            "Yeni tarih",
            null,
            DateTime.UtcNow.AddDays(2),
            60,
            null,
            null);

        action.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Session.NotEditable");
    }

    [Fact]
    public void ScheduledSession_ShouldRejectCancellationAfterItsStart()
    {
        var session = CoachingSession.Create(
            Guid.NewGuid(),
            "Geçmiş seans",
            DateTime.UtcNow.AddMinutes(-5),
            SessionType.OneOnOne);

        var action = () => session.Cancel();

        action.Should().Throw<BusinessRuleException>()
            .Which.Code.Should().Be("Session.NotCancellable");
    }
}
