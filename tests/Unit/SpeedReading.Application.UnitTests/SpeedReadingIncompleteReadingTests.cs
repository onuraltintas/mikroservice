using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.StudentReading;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Sessions;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H5 (finding 16): a reading rejected as too short or too long (no valid speed) does not count in
/// comprehension averages either.
/// </summary>
public sealed class SpeedReadingIncompleteReadingTests
{
    [Fact]
    public async Task Rejected_readings_stay_out_of_comprehension_averages()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var text = ReadingText.Create(Guid.NewGuid(), "Metin", "Kısa bir metin.", difficultyLevel: 1, category: "Genel");
        db.ReadingTexts.Add(text);
        var now = DateTime.UtcNow;
        db.ReadingSessions.AddRange(
            ReadingSession.Import(Guid.NewGuid(), user, text.Id, 60, 200, 4, 5, 80m, 70m, now, now, null, null, null),
            ReadingSession.Import(Guid.NewGuid(), user, text.Id, 1, 0, 0, 5, 0m, 0m, now, now, null, null, null, isMeasured: false));
        await db.SaveChangesAsync();
        var service = (ISpeedReadingStudentReading)Activator.CreateInstance(
            typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentReading")!, db)!;

        (await service.GetStatisticsAsync(user, CancellationToken.None)).AverageComprehension.Should().Be(80);
        (await service.GetComprehensionProgressionAsync(user, CancellationToken.None)).Should().ContainSingle();
    }
}
