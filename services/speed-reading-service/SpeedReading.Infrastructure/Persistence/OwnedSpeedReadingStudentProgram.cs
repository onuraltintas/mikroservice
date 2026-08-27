using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.StudentProgram;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingStudentProgram(OwnedSpeedReadingDbContext db) : ISpeedReadingStudentProgram
{
    public async Task<StudentProgramInfo?> GetMyProgramAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var row = await GetRows(userId)
            .Where(item => item.Progress.IsActive)
            .OrderByDescending(item => item.Progress.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToInfo(row);
    }

    public async Task<IReadOnlyList<StudentProgramInfo>> GetMyProgramsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var rows = await GetRows(userId)
            .OrderByDescending(item => item.Progress.IsActive)
            .ThenByDescending(item => item.Progress.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToInfo).ToList();
    }

    public async Task<StartStudentProgramResult> StartProgramAsync(
        Guid userId,
        Guid templateId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || templateId == Guid.Empty)
            throw new ArgumentException("A valid user and program template are required.");

        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && item.IsActive && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Program not found.");

        var activePrograms = await db.StudentProgramProgresses
            .Where(item => item.UserId == userId && item.IsActive)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        var previous = activePrograms.FirstOrDefault();
        var now = DateTime.UtcNow;

        foreach (var activeProgram in activePrograms)
            activeProgram.Deactivate(userId, now);

        var progress = StudentProgramProgress.Start(
            Guid.NewGuid(),
            userId,
            template,
            previous?.CurrentStreak ?? 0,
            previous?.LongestStreak ?? 0,
            userId,
            now);
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
        join template in db.ProgramTemplates.AsNoTracking()
            on progress.ProgramTemplateId equals template.Id
        join ageGroup in db.AgeGroupConfigurations.AsNoTracking()
            on template.TargetAgeGroupConfigurationId equals ageGroup.Id into ageGroups
        from ageGroup in ageGroups.DefaultIfEmpty()
        where progress.UserId == userId
            && !template.IsDeleted
        select new StudentProgramRow(progress, template, ageGroup == null ? "Bilinmiyor" : ageGroup.DisplayName);

    private static StudentProgramInfo ToInfo(StudentProgramRow row)
    {
        var programTypeName = row.Template.ProgramType == 1 ? "Sınav Hazırlık" : "Standart Program";
        return new StudentProgramInfo(
            row.Progress.Id,
            row.Progress.ProgramTemplateId,
            row.Template.Name,
            row.Template.Description,
            row.Template.ProgramType,
            programTypeName,
            row.Template.ExamType,
            row.Template.TargetAgeGroupConfigurationId,
            row.AgeGroupName,
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

    private sealed record StudentProgramRow(
        StudentProgramProgress Progress,
        ProgramTemplate Template,
        string AgeGroupName);
}
