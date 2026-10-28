using Coaching.Application.Commands.CreateSession;
using Coaching.Application.Commands.DeleteSession;
using Coaching.Application.Commands.UpdateSessionParticipants;
using Coaching.Application.Queries.GetSessions;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

// Roadmap O6: recurring sessions (for example every Tuesday 18:00), changing who takes part after scheduling and the
// teacher's weekly agenda.
public sealed partial class CoachingWriteIdempotencyTests
{
    private static DateTime NextTuesdayAt18Utc()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CoachingTime.Turkey).Date.AddDays(1);
        while (local.DayOfWeek != DayOfWeek.Tuesday) local = local.AddDays(1);
        return TimeZoneInfo.ConvertTimeToUtc(local.AddHours(18), CoachingTime.Turkey);
    }

    private (CreateSessionCommandHandler Handler, InMemorySessionRepository Repository, NoopCoachingEventPublisher Publisher) SeriesSetup()
    {
        var repository = new InMemorySessionRepository();
        var publisher = new NoopCoachingEventPublisher();
        return (new CreateSessionCommandHandler(repository, new CountingUnitOfWork(), new AllowTeacherPolicy(), new AllowIdentityAuthorizationClient(),
            new InMemoryIdempotencyRepository(), publisher), repository, publisher);
    }

    [Fact]
    public async Task AWeeklySeriesSchedulesEveryOccurrenceAtTheSameLocalTime()
    {
        var (handler, repository, publisher) = SeriesSetup();
        var teacher = Guid.NewGuid(); var students = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var start = NextTuesdayAt18Utc();

        var result = await handler.Handle(new CreateSessionCommand(teacher, students[0], start, 60, "Matematik etüdü", null, SessionType.Group,
            "series-key-20261028", students, Recurrence: new SessionRecurrence(1, 4)), CancellationToken.None);

        result.Count.Should().Be(4);
        result.SeriesId.Should().NotBeNull();
        repository.Items.Should().HaveCount(4).And.OnlyContain(item => item.SeriesId == result.SeriesId);
        var local = repository.Items.OrderBy(item => item.ScheduledDate)
            .Select(item => TimeZoneInfo.ConvertTimeFromUtc(item.ScheduledDate, CoachingTime.Turkey)).ToArray();
        local.Should().OnlyContain(time => time.DayOfWeek == DayOfWeek.Tuesday && time.Hour == 18 && time.Minute == 0);
        (local[3] - local[0]).TotalDays.Should().Be(21);
        repository.Items.Should().OnlyContain(item => item.Attendances.Select(a => a.StudentId).OrderBy(id => id).SequenceEqual(students.OrderBy(id => id)));
        publisher.Messages.OfType<SessionScheduledEvent>().Should().ContainSingle("students are told once about the series, not once per week");

        var biweekly = await SeriesSetup().Handler.Handle(new CreateSessionCommand(teacher, students[0], start, 45, "Deneme analizi", null,
            SessionType.OneOnOne, "series-key-biweekly", null, Recurrence: new SessionRecurrence(2, 3)), CancellationToken.None);
        biweekly.Count.Should().Be(3);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 27)]
    [InlineData(3, 4)]
    public async Task ASeriesNeedsAWeeklyOrBiweeklyRhythmAndTwoTo26Sessions(int intervalWeeks, int count)
    {
        var (handler, repository, _) = SeriesSetup();
        var act = () => handler.Handle(new CreateSessionCommand(Guid.NewGuid(), Guid.NewGuid(), NextTuesdayAt18Utc(), 60, "Etüt", null,
            SessionType.OneOnOne, "series-invalid-" + intervalWeeks + count, null, Recurrence: new SessionRecurrence(intervalWeeks, count)), CancellationToken.None);
        await act.Should().ThrowAsync<ValidationException>();
        repository.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task CancellingWithTheFollowingOnesStopsTheRestOfTheSeries()
    {
        var (handler, repository, _) = SeriesSetup();
        var result = await handler.Handle(new CreateSessionCommand(Guid.NewGuid(), Guid.NewGuid(), NextTuesdayAt18Utc(), 60, "Etüt", null,
            SessionType.OneOnOne, "series-cancel-key", null, Recurrence: new SessionRecurrence(1, 4)), CancellationToken.None);
        var ordered = repository.Items.OrderBy(item => item.ScheduledDate).ToArray();
        var publisher = new NoopCoachingEventPublisher();
        var cancel = new SessionDeleteHandlers(repository, new CountingUnitOfWork(), new AllowTeacherPolicy(), publisher, new AllowIdentityAuthorizationClient());

        await cancel.Handle(new CancelSessionCommand(ordered[1].Id) { IncludeFollowing = true }, CancellationToken.None);

        ordered.Select(item => item.Status).Should().Equal(SessionStatus.Scheduled, SessionStatus.Cancelled, SessionStatus.Cancelled, SessionStatus.Cancelled);
        publisher.Messages.OfType<SessionCancelledEvent>().Should().ContainSingle();
        result.SeriesId.Should().Be(ordered[0].SeriesId);
    }

    [Fact]
    public async Task ParticipantsCanBeChangedForOneSessionOrTheRestOfTheSeries()
    {
        var (handler, repository, _) = SeriesSetup();
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        await handler.Handle(new CreateSessionCommand(Guid.NewGuid(), a, NextTuesdayAt18Utc(), 60, "Grup etüdü", null, SessionType.Group,
            "series-participants", [a, b], Recurrence: new SessionRecurrence(1, 3)), CancellationToken.None);
        var ordered = repository.Items.OrderBy(item => item.ScheduledDate).ToArray();
        var publisher = new NoopCoachingEventPublisher();
        var participants = new UpdateSessionParticipantsCommandHandler(repository, new CountingUnitOfWork(), new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(), publisher);

        await participants.Handle(new UpdateSessionParticipantsCommand(ordered[0].Id, [a, c], IncludeFollowing: false), CancellationToken.None);
        ordered[0].Attendances.Select(x => x.StudentId).Should().BeEquivalentTo([a, c]);
        ordered[1].Attendances.Select(x => x.StudentId).Should().BeEquivalentTo([a, b]);
        publisher.Messages.OfType<SessionScheduledEvent>().Single().StudentIds.Should().Equal(c);
        publisher.Messages.OfType<SessionCancelledEvent>().Single().StudentIds.Should().Equal(b);

        await participants.Handle(new UpdateSessionParticipantsCommand(ordered[1].Id, [b], IncludeFollowing: true), CancellationToken.None);
        ordered[1].Attendances.Select(x => x.StudentId).Should().Equal(b);
        ordered[2].Attendances.Select(x => x.StudentId).Should().Equal(b);
        ordered[0].Attendances.Select(x => x.StudentId).Should().BeEquivalentTo([a, c]);

        await participants.Invoking(h => h.Handle(new UpdateSessionParticipantsCommand(ordered[0].Id, [], false), CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        ordered[2].Cancel();
        (await participants.Invoking(h => h.Handle(new UpdateSessionParticipantsCommand(ordered[2].Id, [a], false), CancellationToken.None))
            .Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("Session.NotEditable");
    }

    [Fact]
    public async Task AOneToOneSessionKeepsExactlyOneStudent()
    {
        var (handler, repository, _) = SeriesSetup();
        var a = Guid.NewGuid();
        await handler.Handle(new CreateSessionCommand(Guid.NewGuid(), a, NextTuesdayAt18Utc(), 60, "Birebir", null, SessionType.OneOnOne,
            "one-to-one-participants"), CancellationToken.None);
        var participants = new UpdateSessionParticipantsCommandHandler(repository, new CountingUnitOfWork(), new AllowTeacherPolicy(),
            new AllowIdentityAuthorizationClient(), new NoopCoachingEventPublisher());
        await participants.Invoking(h => h.Handle(new UpdateSessionParticipantsCommand(repository.Items[0].Id, [a, Guid.NewGuid()], false), CancellationToken.None))
            .Should().ThrowAsync<ValidationException>();
        var b = Guid.NewGuid();
        await participants.Handle(new UpdateSessionParticipantsCommand(repository.Items[0].Id, [b], false), CancellationToken.None);
        repository.Items[0].Attendances.Select(x => x.StudentId).Should().Equal(b);
    }

    [Fact]
    public async Task TheWeeklyAgendaListsTheTeachersSessionsOfThatWeek()
    {
        var (handler, repository, _) = SeriesSetup();
        var teacher = Guid.NewGuid();
        var start = NextTuesdayAt18Utc();
        await handler.Handle(new CreateSessionCommand(teacher, Guid.NewGuid(), start, 60, "Etüt", null, SessionType.OneOnOne,
            "agenda-series", null, Recurrence: new SessionRecurrence(1, 3)), CancellationToken.None);
        var monday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(start, CoachingTime.Turkey)).AddDays(-1);
        var query = new GetSessionsQueryHandler(repository, new AllowTeacherPolicy(), new AllowIdentityAuthorizationClient());

        var week = await query.Handle(new GetTeacherAgendaQuery(teacher, monday), CancellationToken.None);

        week.WeekStart.Should().Be(monday);
        week.Sessions.Should().ContainSingle().Which.SeriesId.Should().NotBeNull();
        (await query.Handle(new GetTeacherAgendaQuery(teacher, monday.AddDays(7)), CancellationToken.None)).Sessions.Should().ContainSingle();
        (await query.Handle(new GetTeacherAgendaQuery(teacher, monday.AddDays(21)), CancellationToken.None)).Sessions.Should().BeEmpty();
    }
}
