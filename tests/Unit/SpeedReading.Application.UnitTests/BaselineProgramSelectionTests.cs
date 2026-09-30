using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class BaselineProgramSelectionTests
{
    [Theory]
    [InlineData(0, "Beginner")]
    [InlineData(40, "Middle")]
    [InlineData(40.5, "Middle")]
    [InlineData(70, "Advanced")]
    [InlineData(100, "Advanced")]
    public async Task Initial_assignment_selects_only_general_programs_without_truncating_score(double score, string name)
    {
        var age = Guid.NewGuid();
        await using var db = CreateDb();
        db.ProgramTemplates.AddRange(
            Template(age, "Beginner", 0, 40, 0, 1),
            Template(age, "Middle", 40, 70, 0, 2),
            Template(age, "Advanced", 70, 100, 0, 3),
            Template(age, "Exam", 0, 100, 1, 0, "LGS"),
            Template(age, "Marathon", 0, 100, 3, 0));
        await db.SaveChangesAsync();

        var result = await Select(db, age, (decimal)score);

        result!.Name.Should().Be(name);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(50)]
    public async Task No_matching_general_program_does_not_fall_back_to_unrelated_content(int score)
    {
        var age = Guid.NewGuid();
        await using var db = CreateDb();
        db.ProgramTemplates.AddRange(Template(age, "Beginner", 0, 40, 0, 1),
            Template(age, "Exam", 0, 100, 1, 0, "LGS"),
            Template(Guid.NewGuid(), "Other age", 0, 100, 0, 0));
        await db.SaveChangesAsync();

        (await Select(db, age, score)).Should().BeNull();
    }

    [Fact]
    public async Task Missing_age_does_not_assign_a_program()
    {
        await using var db = CreateDb();
        (await Select(db, null, 0)).Should().BeNull();
    }

    private static OwnedSpeedReadingDbContext CreateDb() => new(
        new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ProgramTemplate Template(Guid age, string name, int min, int max,
        int type, int order, string? exam = null) => ProgramTemplate.Import(
            Guid.NewGuid(), name, "", age, min, max, "{}", 1, 2, 5, 4, 28,
            true, order, type, exam, false, DateTime.UtcNow, null, null, null);

    private static async Task<ProgramTemplate?> Select(OwnedSpeedReadingDbContext db, Guid? age, decimal score)
    {
        var type = typeof(OwnedSpeedReadingDbContext).Assembly.GetType(
            "SpeedReading.Infrastructure.Persistence.OwnedSpeedReadingAssessment")!;
        var service = Activator.CreateInstance(type, db, null)!;
        var method = type.GetMethod("FindBaselineTemplateAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return await (Task<ProgramTemplate?>)method.Invoke(service, [age, score, CancellationToken.None])!;
    }
}
