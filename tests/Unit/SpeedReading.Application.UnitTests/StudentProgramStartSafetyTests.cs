using EduPlatform.Shared.Kernel.Exceptions;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class StudentProgramStartSafetyTests
{
    [Fact]
    public async Task First_program_cannot_be_started_by_bypassing_assessment_placement()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var template = Template(user);
        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync();
        var start = () => Service(db).StartProgramAsync(user, template.Id, CancellationToken.None);
        (await start.Should().ThrowAsync<BusinessRuleException>()).Which.Code
            .Should().Be("Program.AssessmentPlacementRequired");
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Repeated_start_returns_existing_active_assignment_without_resetting_progress()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var template = Template(user);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, template, 3, 5, user, DateTime.UtcNow);
        db.ProgramTemplates.Add(template);
        db.StudentProgramProgresses.Add(progress);
        await db.SaveChangesAsync();

        var result = await Service(db).StartProgramAsync(user, template.Id, CancellationToken.None);

        result.ProgramId.Should().Be(progress.Id);
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(1);
        progress.IsActive.Should().BeTrue();
        progress.CurrentStreak.Should().Be(3);
    }

    [Fact]
    public async Task Different_program_cannot_silently_replace_an_unfinished_program()
    {
        await using var db = CreateDb();
        var user = Guid.NewGuid();
        var first = Template(user);
        var other = Template(user);
        var progress = StudentProgramProgress.Start(Guid.NewGuid(), user, first, 0, 0, user, DateTime.UtcNow);
        db.ProgramTemplates.AddRange(first, other);
        db.StudentProgramProgresses.Add(progress);
        await db.SaveChangesAsync();
        var start = () => Service(db).StartProgramAsync(user, other.Id, CancellationToken.None);

        await start.Should().ThrowAsync<BusinessRuleException>();
        progress.IsActive.Should().BeTrue();
        (await db.StudentProgramProgresses.CountAsync()).Should().Be(1);
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(
        new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ISpeedReadingStudentProgram Service(OwnedSpeedReadingDbContext db) =>
        (ISpeedReadingStudentProgram)Activator.CreateInstance(typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingStudentProgram")!, db, null, null)!;

    private static ProgramTemplate Template(Guid user) => ProgramTemplate.Import(Guid.NewGuid(), "Program", "",
        Guid.NewGuid(), 0, 100, "{}", 1, 2, 5, 1, 1, true, 1, 0, null, false,
        DateTime.UtcNow, user.ToString(), null, null);
}
