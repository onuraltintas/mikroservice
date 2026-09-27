using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Review;
using SpeedReading.Domain.Review;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class OwnedSpeedReadingReviewHistoryTests
{
    [Fact]
    public async Task Completed_session_is_recorded_once_with_its_actual_score()
    {
        var userId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var item = ReviewItem.Start(Guid.NewGuid(), userId, exerciseId, null,
            DateTime.UtcNow.AddDays(-2), userId);
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
