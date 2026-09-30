using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class StudentProgramMetadataTests
{
    [Fact]
    public async Task Progress_includes_name_and_frozen_assignment_duration()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = Guid.NewGuid();
        var template = ProgramTemplate.Import(Guid.NewGuid(), "My program", "", Guid.NewGuid(), 0, 100,
            "{}", 1, 2, 5, 4, 28, true, 1, 0, null, false, DateTime.UtcNow, null, null, null);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 0, 0, user, DateTime.UtcNow);
        progress.SetSchedule("[{\"weekNumber\":1,\"dayNumber\":1,\"order\":1,\"exerciseId\":\"11111111-1111-1111-1111-111111111111\"}]", user, DateTime.UtcNow);
        db.ProgramTemplates.Add(template);
        db.StudentProgramProgresses.Add(progress);
        await db.SaveChangesAsync();
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingPrograms")!;
        var service = (ILegacySpeedReadingPrograms)Activator.CreateInstance(type, db, null, null)!;

        var result = (await service.GetStudentProgressAsync(user, CancellationToken.None)).Single();

        result.TemplateName.Should().Be("My program");
        result.TotalDays.Should().Be(1);
        result.TotalWeeks.Should().Be(1);
    }
}
