using System.Text.Json;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed record ScheduledProgramExercise(int WeekNumber, int DayNumber, int Order, Guid ExerciseId);

internal static class OwnedSpeedReadingProgramSchedule
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<ScheduledProgramExercise> Parse(string? json) =>
        string.IsNullOrWhiteSpace(json)
            ? []
            : JsonSerializer.Deserialize<List<ScheduledProgramExercise>>(json, JsonOptions) ?? [];

    public static async Task<string> BuildAsync(
        OwnedSpeedReadingDbContext db,
        ProgramTemplate template,
        Guid? existingProgressId,
        CancellationToken cancellationToken)
    {
        var exerciseTypes = await db.ExerciseTypes.AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var exercises = await db.Exercises.AsNoTracking()
            .Where(item => !item.IsDeleted
                && (item.TargetAgeGroupId == null
                    || item.TargetAgeGroupId == template.TargetAgeGroupConfigurationId))
            .ToListAsync(cancellationToken);
        var completedLogs = existingProgressId.HasValue
            ? await db.DailyExerciseLogs.AsNoTracking()
                .Where(item => item.StudentProgramProgressId == existingProgressId.Value)
                .OrderBy(item => item.CompletedDate)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken)
            : [];

        var schedule = new List<ScheduledProgramExercise>();
        var totalDays = template.TotalDays > 0 ? template.TotalDays : template.TotalWeeks * 7;
        for (var cumulativeDay = 1; cumulativeDay <= totalDays; cumulativeDay++)
        {
            var (week, day) = SpeedReadingDailyProgressRules.GetWeekAndDay(cumulativeDay);
            var patternJson = SpeedReadingDailyProgressRules.GetDailyPatternJson(template.WeeklyPatternJson, week, day);
            var patterns = patternJson is null
                ? []
                : JsonSerializer.Deserialize<List<SchedulePattern>>(patternJson, JsonOptions) ?? [];
            if (!patterns.Any(pattern => pattern.Count > 0 && !string.IsNullOrWhiteSpace(pattern.Type)))
                throw new BusinessRuleException(
                    "Program.DayEmpty",
                    $"'{template.Name}' programının {week}. hafta {day}. günü için egzersiz tanımlanmamış.");
            var order = 1;
            var usedExerciseIds = new HashSet<Guid>();
            foreach (var pattern in patterns)
            {
                if (pattern.Count <= 0 || string.IsNullOrWhiteSpace(pattern.Type))
                    continue;
                var exerciseType = exerciseTypes.SingleOrDefault(item => item.Name == pattern.Type)
                    ?? throw new BusinessRuleException(
                        "Program.ExerciseTypeMissing",
                        $"'{template.Name}' programındaki '{pattern.Type}' egzersiz türü bulunamadı.");
                var difficulty = SpeedReadingDailyProgressRules.ResolveExerciseDifficulty(
                    pattern.Difficulty,
                    template.InitialDifficultyLevel,
                    template.InitialDifficultyLevel,
                    template.MaxDifficultyLevel);
                var completedIds = completedLogs
                    .Where(item => item.WeekNumber == week
                        && item.DayNumber == day
                        && item.ExerciseTypeId == exerciseType.Id
                        && !usedExerciseIds.Contains(item.ExerciseId))
                    .Select(item => item.ExerciseId);
                var selected = completedIds
                    .Concat(exercises
                        .Where(item => item.IsActive
                            && item.ExerciseTypeId == exerciseType.Id
                            && !usedExerciseIds.Contains(item.Id)
                            && item.DifficultyLevel <= difficulty)
                        .OrderByDescending(item => item.DifficultyLevel)
                        .ThenBy(item => item.Id)
                        .Select(item => item.Id))
                    .Distinct()
                    .Take(pattern.Count)
                    .ToList();
                if (selected.Count != pattern.Count)
                {
                    throw new BusinessRuleException(
                        "Program.ContentMissing",
                        $"'{template.Name}' programının {week}. hafta {day}. gününde "
                        + $"'{pattern.Type}' için {pattern.Count} farklı egzersiz gerekli; {selected.Count} bulundu.");
                }

                foreach (var exerciseId in selected)
                {
                    usedExerciseIds.Add(exerciseId);
                    schedule.Add(new ScheduledProgramExercise(week, day, order++, exerciseId));
                }
            }
        }

        return JsonSerializer.Serialize(schedule);
    }

    private sealed class SchedulePattern
    {
        public string Type { get; set; } = string.Empty;
        public int Count { get; set; }
        public int Difficulty { get; set; }
    }
}
