using Microsoft.EntityFrameworkCore;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingTrainingProgramAudit(OwnedSpeedReadingDbContext db)
{
    public async Task<TrainingProgramAuditReport> RunAsync(CancellationToken cancellationToken = default)
    {
        var templates = await db.ProgramTemplates.AsNoTracking()
            .Where(p => p.IsActive && !p.IsDeleted && !p.IsAssessment).ToListAsync(cancellationToken);
        var exercises = await db.Exercises.AsNoTracking().ToDictionaryAsync(e => e.Id, cancellationToken);
        var days = 0;
        var slots = 0;
        foreach (var template in templates)
        {
            var schedule = OwnedSpeedReadingProgramSchedule.Parse(await OwnedSpeedReadingProgramSchedule.BuildAsync(db, template, null, cancellationToken));
            foreach (var entry in schedule)
            {
                var exercise = exercises[entry.ExerciseId];
                if (exercise.IsAssessmentOnly || exercise.IsDeleted || !exercise.IsActive
                    || exercise.DifficultyLevel != template.InitialDifficultyLevel
                    || exercise.TargetAgeGroupId != template.TargetAgeGroupConfigurationId)
                    throw new InvalidOperationException($"Normal program has an invalid age/level/scope selection: {template.Id}.");
            }
            days += schedule.Select(s => (s.WeekNumber, s.DayNumber)).Distinct().Count();
            slots += schedule.Count;
        }
        return new TrainingProgramAuditReport(templates.Count, days, slots);
    }
}

public sealed record TrainingProgramAuditReport(int Programs, int Days, int Slots);
