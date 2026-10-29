using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H5 (finding 18): rebuilding XP from history gives the same XP the student received live — the awarded XP
/// and whether the session counted are stored with the result.
/// </summary>
public sealed class SpeedReadingXpRecalculationTests
{
    [Fact]
    public async Task Recalculation_uses_the_xp_awarded_live_and_skips_sessions_that_did_not_count()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var type = ExerciseType.Create(Guid.NewGuid(), "RSVP", "RSVP", "text_stream");
        var exercise = Exercise.Create("Okuma", "rsvp", "{}", 1, student, type.Id);
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(exercise);
        var counted = Result(student, exercise.Id, score: 90);
        counted.RecordGamification(countsForGamification: true, xpAwarded: 37);
        var incomplete = Result(student, exercise.Id, score: 95);
        incomplete.RecordGamification(countsForGamification: false, xpAwarded: 0);
        db.ExerciseSessionResults.AddRange(counted, incomplete);
        await db.SaveChangesAsync();

        await new OwnedSpeedReadingGamificationRecalculation(db).RunAsync();

        var stats = await db.UserGamifications.SingleAsync(item => item.UserId == student);
        stats.TotalXP.Should().Be(37);
        stats.TotalActivitiesCompleted.Should().Be(1);
    }

    private static ExerciseSessionResult Result(Guid student, Guid exercise, decimal score) =>
        ExerciseSessionResult.Create(Guid.NewGuid(), Guid.NewGuid(), student, exercise, null, 200, 60, 200m, 80m, 160m, score,
            DateTime.UtcNow, "[]", "[]", false, true);
}
