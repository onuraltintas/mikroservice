using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;
using SpeedReading.Infrastructure.Privacy;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H4b (finding 22, decision 29.10.2026): finished exercise sessions keep their raw working state for 90 days,
/// then only the flags reports still need; read notifications and campaign send logs are kept for a year.
/// Results, scores and answers are never touched.
/// </summary>
public sealed class SpeedReadingDataRetentionTests
{
    private static readonly DateTime Now = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Old_session_state_is_compacted_and_old_read_notifications_and_logs_are_removed()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var bigState = JsonSerializer.Serialize(new { comprehensionMode = "learning", readingIncomplete = true, text = new string('k', 5000) });
        var old = Session(ExerciseSessionStatus.Completed, Now.AddDays(-100), bigState);
        var recent = Session(ExerciseSessionStatus.Completed, Now.AddDays(-10), bigState);
        var active = Session(ExerciseSessionStatus.Active, null, bigState, Now.AddDays(-200));
        db.ExerciseSessions.AddRange(old, recent, active);
        Add(db, "LegacyUserNotification", new { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "eski okunmuş", Message = "m", ReadAt = (DateTime?)Now.AddDays(-399), CreatedAt = Now.AddDays(-400) });
        Add(db, "LegacyUserNotification", new { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "eski okunmamış", Message = "m", ReadAt = (DateTime?)null, CreatedAt = Now.AddDays(-400) });
        Add(db, "LegacyUserNotification", new { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Title = "yeni okunmuş", Message = "m", ReadAt = (DateTime?)Now.AddDays(-99), CreatedAt = Now.AddDays(-100) });
        Add(db, "LegacyEmailCampaignLog", new { Id = Guid.NewGuid(), CampaignId = Guid.NewGuid(), RecipientEmail = "a@example.test", Status = "Sent", CreatedAt = Now.AddDays(-400) });
        Add(db, "LegacyEmailCampaignLog", new { Id = Guid.NewGuid(), CampaignId = Guid.NewGuid(), RecipientEmail = "b@example.test", Status = "Sent", CreatedAt = Now.AddDays(-100) });
        await db.SaveChangesAsync();

        await new SpeedReadingDataRetentionProcessor(db, new FixedClock(Now)).RunAsync(CancellationToken.None);

        db.ChangeTracker.Clear();
        var compacted = await db.ExerciseSessions.SingleAsync(item => item.Id == old.Id);
        using (var state = JsonDocument.Parse(compacted.SessionDataJson))
        {
            state.RootElement.GetProperty("comprehensionMode").GetString().Should().Be("learning");
            state.RootElement.GetProperty("readingIncomplete").GetBoolean().Should().BeTrue();
            state.RootElement.TryGetProperty("text", out _).Should().BeFalse();
        }
        compacted.ProcessedActionsJson.Should().Be("{}");
        (await db.ExerciseSessions.SingleAsync(item => item.Id == recent.Id)).SessionDataJson.Should().Be(bigState);
        (await db.ExerciseSessions.SingleAsync(item => item.Id == active.Id)).SessionDataJson.Should().Be(bigState);
        Titles(db, "LegacyUserNotification").Should().BeEquivalentTo(["eski okunmamış", "yeni okunmuş"]);
        Titles(db, "LegacyEmailCampaignLog", "RecipientEmail").Should().Equal("b@example.test");
    }

    private static ExerciseSession Session(ExerciseSessionStatus status, DateTime? endTime, string state, DateTime? start = null) =>
        ExerciseSession.Import(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, null, status, start ?? endTime!.Value.AddMinutes(-5), endTime, 0, null, null,
            1, 1, 1, 0, state, null, "{\"a\":{\"isValid\":true}}", start ?? endTime!.Value, null, null, null);

    private static void Add(OwnedSpeedReadingDbContext db, string name, object values)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var entity = Activator.CreateInstance(type)!;
        foreach (var property in values.GetType().GetProperties())
            type.GetProperty(property.Name)!.SetValue(entity, property.GetValue(values));
        db.Add(entity);
    }

    private static List<string> Titles(OwnedSpeedReadingDbContext db, string name, string property = "Title")
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType($"SpeedReading.Infrastructure.Legacy.{name}")!;
        var set = (IQueryable<object>)typeof(DbContext).GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(type).Invoke(db, null)!;
        return set.AsEnumerable().Select(row => (string)type.GetProperty(property)!.GetValue(row)!).ToList();
    }

    private sealed class FixedClock(DateTime now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(now);
    }
}
