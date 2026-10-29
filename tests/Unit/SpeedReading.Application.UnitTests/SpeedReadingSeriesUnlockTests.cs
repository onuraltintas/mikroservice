using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.SeriesAccess;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

/// <summary>
/// Review H3 (finding 12): a student cannot open any series by themselves and close their current program. New
/// programs start only through the program flow (placement / post-training measurement and, where required, the
/// teacher's approval); series access only reports what is already open.
/// </summary>
public sealed class SpeedReadingSeriesUnlockTests
{
    [Fact]
    public async Task A_student_cannot_open_a_new_series_and_their_program_stays_active()
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var student = Guid.NewGuid();
        var current = Template("Başlangıç", 1);
        var advanced = Template("İleri", 5);
        db.ProgramTemplates.AddRange(current, advanced);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), student, current, 0, 0, student, DateTime.UtcNow);
        db.StudentProgramProgresses.Add(progress);
        await db.SaveChangesAsync();
        var service = Create(db);

        var unlock = () => service.UnlockAsync(student, advanced.Id, CancellationToken.None);

        var refused = await unlock.Should().ThrowAsync<BusinessRuleException>();
        refused.Which.Code.Should().Be("Series.ProgramFlowRequired");
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(1);
        (await db.StudentProgramProgresses.SingleAsync()).IsActive.Should().BeTrue();
        (await service.UnlockAsync(student, current.Id, CancellationToken.None))!.Success.Should().BeTrue();
    }

    private static ISpeedReadingSeriesAccess Create(OwnedSpeedReadingDbContext db)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingSeriesAccess")!;
        return (ISpeedReadingSeriesAccess)Activator.CreateInstance(type, db)!;
    }

    private static ProgramTemplate Template(string name, int order) => ProgramTemplate.Create(
        name: name,
        description: name,
        targetAgeGroupConfigurationId: Guid.NewGuid(),
        minAssessmentScore: 0,
        maxAssessmentScore: 100,
        weeklyPatternJson: "{\"week1\":[{\"type\":\"RSVP\",\"count\":1,\"difficulty\":1}]}",
        initialDifficultyLevel: 1,
        weeksPerDifficultyIncrease: 2,
        maxDifficultyLevel: 5,
        totalWeeks: 4,
        totalDays: 28,
        isActive: true,
        displayOrder: order,
        programType: 1,
        examType: null,
        isAssessment: false,
        actorId: Guid.NewGuid(),
        createdAt: DateTime.UtcNow);
}
