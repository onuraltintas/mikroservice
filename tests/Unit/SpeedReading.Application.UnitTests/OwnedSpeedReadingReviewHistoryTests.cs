using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Review;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Review;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class OwnedSpeedReadingReviewHistoryTests
{
    [Fact]
    public async Task Session_completed_before_review_was_due_cannot_advance_schedule()
    {
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var item = ReviewItem.Start(Guid.NewGuid(), userId, exerciseId, null,
            DateTime.UtcNow, userId);
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ReviewItems.Add(item);
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(
            Guid.NewGuid(), sessionId, userId, exerciseId, null,
            0, 0, 0, 0, 0, 82, DateTime.UtcNow));
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingReview")!;
        var review = (ISpeedReadingReview)Activator.CreateInstance(type, db)!;

        var result = await review.SubmitAsync(userId, item.Id, sessionId, CancellationToken.None);

        result.Should().BeNull();
        item.ReviewCount.Should().Be(0);
    }

    [Fact]
    public async Task Completed_session_is_recorded_once_with_its_actual_score()
    {
        var userId = Guid.NewGuid();
        var exercise = Exercise.Create("Review exercise", "SpeedReading", "{}", 1, userId, Guid.NewGuid());
        var exerciseId = exercise.Id;
        var sessionId = Guid.NewGuid();
        var item = ReviewItem.Start(Guid.NewGuid(), userId, exerciseId, null,
            DateTime.UtcNow.AddDays(-2), userId);
        await using var db = new OwnedSpeedReadingDbContext(
            new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.ReviewItems.Add(item);
        db.Exercises.Add(exercise);
        db.ExerciseSessionResults.Add(ExerciseSessionResult.Create(
            Guid.NewGuid(), sessionId, userId, exerciseId, null,
            0, 0, 0, 0, 0, 82, DateTime.UtcNow));
        await db.SaveChangesAsync();

        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingReview")!;
        var review = (ISpeedReadingReview)Activator.CreateInstance(type, db)!;

        var first = await review.SubmitAsync(userId, item.Id, sessionId, CancellationToken.None);
        var second = await review.SubmitAsync(userId, item.Id, sessionId, CancellationToken.None);
        var history = await review.GetHistoryAsync(userId, exerciseId, CancellationToken.None);

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        history.Should().ContainSingle();
        history[0].Score.Should().Be(82);
        item.ReviewCount.Should().Be(1);

        var allItems = await review.GetAllAsync(userId, CancellationToken.None);
        allItems.Should().ContainSingle(summary => summary.ReviewItemId == item.Id);
    }
}
