using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Application.UnitTests;

public sealed class TrainingProgramAuditTests
{
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task Readonly_audit_detects_lower_level_fallback(int requestedLevel, bool rejects)
    {
        await using var db = new OwnedSpeedReadingDbContext(new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var type = ExerciseType.Create(Guid.NewGuid(), "Fixation", "Sabitleme", "motion_path");
        var age = Guid.NewGuid();
        db.ExerciseTypes.Add(type);
        db.Exercises.Add(Exercise.Create("Exercise", type.Name, "{}", 1, Guid.NewGuid(), type.Id, targetAgeGroupId: age));
        db.ProgramTemplates.Add(ProgramTemplate.Import(Guid.NewGuid(), "Program", "", age, 0, 100,
            $"{{\"week1\":{{\"day1\":[{{\"type\":\"Fixation\",\"count\":1,\"difficulty\":{requestedLevel}}}]}}}}",
            requestedLevel, 0, requestedLevel, 1, 1, true, 1, 0, null, false, DateTime.UtcNow, null, null, null));
        await db.SaveChangesAsync();
        var audit = new OwnedSpeedReadingTrainingProgramAudit(db);
        if (rejects) await Assert.ThrowsAsync<InvalidOperationException>(() => audit.RunAsync());
        else
        {
            var report = await audit.RunAsync();
            Assert.Equal(1, report.Programs); Assert.Equal(1, report.Slots);
            Assert.Empty(db.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged));
        }
    }
}
