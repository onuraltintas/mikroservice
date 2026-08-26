using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.StudentProgram;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingStudentProgram(SpeedReadingDbContext db) : ISpeedReadingStudentProgram
{
    public async Task<StudentProgramInfo?> GetMyProgramAsync(Guid userId, CancellationToken cancellationToken)
    {
        var row = await GetRows(userId)
            .Where(item => item.Progress.IsActive)
            .OrderByDescending(item => item.Progress.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : await ToInfoAsync(row, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentProgramInfo>> GetMyProgramsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await GetRows(userId)
            .OrderByDescending(item => item.Progress.IsActive)
            .ThenByDescending(item => item.Progress.CreatedAt)
            .ToListAsync(cancellationToken);

        var ageNames = await GetAgeNamesAsync(rows.Select(item => item.Template.TargetAgeGroupConfigurationId), cancellationToken);
        return rows.Select(row => ToInfo(row, ageNames)).ToList();
    }

    public async Task<StartStudentProgramResult> StartProgramAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        var template = await db.ExerciseProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (template is null)
        {
            throw new KeyNotFoundException("Program not found.");
        }

        var activePrograms = await db.StudentProgramProgresses
            .Where(item => item.UserId == userId && item.IsActive && !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var previous = activePrograms.FirstOrDefault();
        var now = DateTime.UtcNow;

        foreach (var activeProgram in activePrograms)
        {
            activeProgram.IsActive = false;
            activeProgram.UpdatedAt = now;
            activeProgram.UpdatedBy = userId;
        }

        var progress = new LegacyStudentProgramProgress
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProgramTemplateId = template.Id,
            AssignedDate = now,
            CurrentDay = 1,
            CurrentWeek = 1,
            CurrentDifficultyLevel = template.InitialDifficultyLevel,
            DaysCompleted = 0,
            ExercisesCompleted = 0,
            AverageSuccessRate = 0,
            CurrentStreak = previous?.CurrentStreak ?? 0,
            LongestStreak = previous?.LongestStreak ?? 0,
            IsActive = true,
            CreatedAt = now,
            CreatedBy = userId
        };

        db.StudentProgramProgresses.Add(progress);
        await db.SaveChangesAsync(cancellationToken);
        return new StartStudentProgramResult(
            true,
            progress.Id,
            template.Name,
            $"'{template.Name}' programına başladınız!");
    }

    private IQueryable<StudentProgramRow> GetRows(Guid userId) =>
        from progress in db.StudentProgramProgresses.AsNoTracking()
        join template in db.ExerciseProgramTemplates.AsNoTracking()
            on progress.ProgramTemplateId equals template.Id
        where progress.UserId == userId && !progress.IsDeleted && !template.IsDeleted
        select new StudentProgramRow(progress, template);

    private async Task<StudentProgramInfo> ToInfoAsync(
        StudentProgramRow row,
        CancellationToken cancellationToken)
    {
        var ageNames = await GetAgeNamesAsync([row.Template.TargetAgeGroupConfigurationId], cancellationToken);
        return ToInfo(row, ageNames);
    }

    private static StudentProgramInfo ToInfo(
        StudentProgramRow row,
        IReadOnlyDictionary<Guid, string> ageNames)
    {
        var programTypeName = row.Template.ProgramType == 1 ? "Sınav Hazırlık" : "Standart Program";
        var ageName = ageNames.TryGetValue(row.Template.TargetAgeGroupConfigurationId, out var name)
            ? name
            : "Bilinmiyor";
        return new StudentProgramInfo(
            row.Progress.Id,
            row.Progress.ProgramTemplateId,
            row.Template.Name,
            row.Template.Description,
            row.Template.ProgramType,
            programTypeName,
            row.Template.ExamType,
            row.Template.TargetAgeGroupConfigurationId,
            ageName,
            row.Template.MinAssessmentScore,
            row.Template.MaxAssessmentScore,
            row.Progress.CurrentWeek,
            row.Progress.CurrentDay,
            row.Progress.CurrentDifficultyLevel,
            row.Template.MaxDifficultyLevel,
            row.Progress.DaysCompleted,
            row.Progress.ExercisesCompleted,
            row.Progress.AverageSuccessRate,
            row.Progress.CurrentStreak,
            row.Progress.LongestStreak,
            row.Progress.AssignedDate,
            row.Progress.LastCompletionDate,
            row.Progress.IsActive,
            row.Progress.CompletedDate);
    }

    private async Task<Dictionary<Guid, string>> GetAgeNamesAsync(
        IEnumerable<Guid> ageGroupIds,
        CancellationToken cancellationToken)
    {
        var ids = ageGroupIds.Distinct().ToArray();
        return await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => ids.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
    }

    private sealed record StudentProgramRow(
        LegacyStudentProgramProgress Progress,
        LegacyExerciseProgramTemplate Template);
}
