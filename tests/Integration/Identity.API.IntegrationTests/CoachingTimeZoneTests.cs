using Coaching.API.Controllers;
using Coaching.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.IntegrationTests;

public sealed class CoachingTimeZoneTests
{
    [Fact]
    public async Task SettingsChangePreservesWindowsAndSlotsAndOtherStudent()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var id = f.AddAccount("Student"); f.Actor.UserId = id;
        var hours = StudyAvailability.Create(id, "Europe/Istanbul");
        hours.ReplaceWindows([new(DayOfWeek.Monday, 480, 540)]);
        var draft = WeeklyStudyDraft.Create(id);
        var slot = new WeeklyDraftSlot(Guid.NewGuid(), Guid.NewGuid(), "Matematik", "LGS", DayOfWeek.Monday, 480, 60);
        draft.Replace("Europe/Istanbul", [slot]);
        var other = StudyAvailability.Create(Guid.NewGuid(), "Europe/Istanbul");
        f.Db.StudyAvailability.AddRange(hours, other); f.Db.Set<WeeklyStudyDraft>().Add(draft);
        var revision = StudyPlanRevision.Create(id, Guid.NewGuid(), 1, "Korunan plan"); revision.Activate();
        var task = StudyPlanTask.Create(revision, new(2026, 10, 23), "Tamamlanan çalışma", 30, plannedQuestions: 10);
        task.Complete(25, 8); f.Db.AddRange(revision, task);
        await f.Db.SaveChangesAsync();
        var taskVersion = task.Version;
        var controller = new CoachingProfilesController(f.Db, f.Store, f.Accounts, f.Actor);
        Assert.IsType<OkObjectResult>(await controller.UpdateTimeZone(new("Europe/Berlin", hours.Version, draft.Version), default));
        Assert.Equal("Europe/Berlin", hours.TimeZoneId);
        Assert.Equal("Europe/Berlin", draft.TimeZoneId);
        Assert.Equal(slot, Assert.Single(draft.Slots));
        Assert.Equal(480, Assert.Single(hours.Windows).StartMinute);
        Assert.Equal("Europe/Istanbul", other.TimeZoneId);
        Assert.True(task.IsCompleted); Assert.Equal(25, task.ActualMinutes); Assert.Equal(8, task.ActualQuestions);
        Assert.Equal(new DateOnly(2026, 10, 23), task.PlannedDate); Assert.Equal(taskVersion, task.Version);
    }

    [Fact]
    public async Task InvalidAndStaleSettingsDoNotMutateData()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var id = f.AddAccount("Student"); f.Actor.UserId = id;
        var hours = StudyAvailability.Create(id, "Europe/Istanbul"); f.Db.StudyAvailability.Add(hours);
        await f.Db.SaveChangesAsync();
        var controller = new CoachingProfilesController(f.Db, f.Store, f.Accounts, f.Actor);
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateTimeZone(new("Invalid/Zone", hours.Version, null), default));
        Assert.IsType<BadRequestObjectResult>(await controller.UpdateTimeZone(new("Turkey Standard Time", hours.Version, null), default));
        Assert.IsType<ConflictObjectResult>(await controller.UpdateTimeZone(new("Europe/Berlin", null, null), default));
        Assert.Equal("Europe/Istanbul", hours.TimeZoneId);
    }
}

[Collection("Database")]
public sealed class CoachingTimeZonePostgresTests(Shared.IntegrationTests.Fixtures.PostgresFixture postgres)
{
    [Fact]
    public async Task ConcurrentInitialSettingsSaveHasOneWinner()
    {
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        Assert.Equal(postgres.DatabaseName, connection.Database);
        Assert.Equal(postgres.Port, connection.Port);
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Coaching.Infrastructure.Data.CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, x => x.EnableRetryOnFailure()).Options;
        await using var db = new Coaching.Infrastructure.Data.CoachingDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var id = f.AddAccount("Student"); f.Actor.UserId = id;
        await using var first = new Coaching.Infrastructure.Data.CoachingDbContext(options);
        await using var second = new Coaching.Infrastructure.Data.CoachingDbContext(options);
        var a = new CoachingProfilesController(first, f.Store, f.Accounts, f.Actor);
        var b = new CoachingProfilesController(second, f.Store, f.Accounts, f.Actor);
        var responses = await Task.WhenAll(a.UpdateTimeZone(new("Europe/London", null, null), default),
            b.UpdateTimeZone(new("Europe/Berlin", null, null), default));
        Assert.Single(responses.OfType<OkObjectResult>());
        Assert.Single(responses.OfType<ConflictObjectResult>());
        Assert.Single(await db.StudyAvailability.Where(x => x.StudentId == id).ToArrayAsync());
    }
}
