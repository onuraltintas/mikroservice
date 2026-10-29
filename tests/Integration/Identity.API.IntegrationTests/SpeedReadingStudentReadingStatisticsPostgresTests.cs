using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;
using SpeedReading.Application.AdaptiveLearning;
using SpeedReading.Application.Review;
using SpeedReading.Application.StudentReading;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace Identity.API.IntegrationTests;

/// <summary>Review H4 (finding 23): reading statistics are aggregated by PostgreSQL (no full history load).</summary>
[Collection("Database")]
public sealed class SpeedReadingStudentReadingStatisticsPostgresTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Statistics_and_history_are_computed_by_the_database()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        try
        {
            await db.Database.EnsureCreatedAsync();
            var user = Guid.NewGuid();
            var text = ReadingText.Create(Guid.NewGuid(), "Göç", "Kuşlar güneye göç eder.", difficultyLevel: 2, category: "Bilim");
            db.ReadingTexts.Add(text);
            var now = DateTime.UtcNow;
            db.ReadingSessions.AddRange(
                ReadingSession.Import(Guid.NewGuid(), user, text.Id, 120, 240, 4, 5, 80m, 70m, now.AddDays(-1), now, null, null, null),
                ReadingSession.Import(Guid.NewGuid(), user, text.Id, 60, 300, 5, 5, 100m, 90m, now, now, null, null, null),
                ReadingSession.Import(Guid.NewGuid(), Guid.NewGuid(), text.Id, 60, 900, 0, 0, 0m, 0m, now, now, null, null, null));
            await db.SaveChangesAsync();
            var service = (ISpeedReadingStudentReading)Activator.CreateInstance(
                typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentReading")!, db)!;

            var statistics = await service.GetStatisticsAsync(user, CancellationToken.None);
            var history = await service.GetHistoryAsync(user, null, null, null, "Bilim", CancellationToken.None);

            statistics.TotalSessions.Should().Be(2);
            statistics.AverageWPM.Should().Be(270);
            statistics.AverageComprehension.Should().Be(90);
            statistics.TextsCompleted.Should().Be(1);
            statistics.TotalTimeMinutes.Should().Be(3);
            statistics.CategoriesRead.Should().Equal("Bilim");
            statistics.RecentSessions.Should().HaveCount(2);
            history.Should().HaveCount(2).And.OnlyContain(item => item.ReadingTextTitle == "Göç");

            var review = (ISpeedReadingReview)Activator.CreateInstance(
                typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingReview")!, db)!;
            (await review.GetStatisticsAsync(user, null, CancellationToken.None)).Should().NotBeNull();
            var adaptive = (ISpeedReadingAdaptiveLearning)Activator.CreateInstance(
                typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAdaptiveLearning")!, db)!;
            (await adaptive.GetProfileAsync(user, CancellationToken.None)).TotalReadingSessions.Should().Be(2);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
