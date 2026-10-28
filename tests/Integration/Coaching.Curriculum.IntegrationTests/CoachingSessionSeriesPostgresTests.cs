using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap O6: on PostgreSQL a changed participant list inserts and deletes attendance rows of a tracked session, and a
// series is read back in order.
public sealed class CoachingSessionSeriesPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ParticipantsAndSeriesRunOnPostgres()
    {
        await using var f = await CurriculumPostgresTests.Pg.Create();
        var teacher = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var series = Guid.NewGuid();
        var start = DateTime.UtcNow.AddDays(3);
        for (var w = 0; w < 3; w++)
        {
            var session = CoachingSession.Create(teacher, "Grup etüdü", start.AddDays(7 * w), SessionType.Group, 60);
            session.AddStudents([a, b]); session.AssignSeries(series);
            f.Db.Add(session);
        }
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var repository = new CoachingSessionRepository(f.Db);
        var loaded = await repository.GetSeriesAsync(series);
        Assert.Equal(3, loaded.Count);
        Assert.True(loaded.Zip(loaded.Skip(1)).All(pair => pair.First.ScheduledDate < pair.Second.ScheduledDate));
        var (added, removed) = loaded[0].SetParticipants([a, c], DateTime.UtcNow);
        Assert.Equal(new List<Guid> { c }, added.ToList()); Assert.Equal(new List<Guid> { b }, removed.ToList());
        await f.Db.SaveChangesAsync();
        f.Db.ChangeTracker.Clear();

        var reloaded = await f.Db.CoachingSessions.Include(x => x.Attendances).SingleAsync(x => x.Id == loaded[0].Id);
        Assert.Equal(new[] { a, c }.OrderBy(x => x), reloaded.Attendances.Select(x => x.StudentId).OrderBy(x => x));
        Assert.Equal(3, await f.Db.CoachingSessions.CountAsync(x => x.SeriesId == series));
        var week = await repository.GetByTeacherIdInRangeAsync(teacher, start.AddDays(-1), start.AddDays(6));
        Assert.Single(week);
    }
}
