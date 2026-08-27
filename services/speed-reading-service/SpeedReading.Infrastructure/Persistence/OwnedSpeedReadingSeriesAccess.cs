using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.SeriesAccess;
using SpeedReading.Domain.Programs;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingSeriesAccess(OwnedSpeedReadingDbContext db)
    : ISpeedReadingSeriesAccess
{
    public async Task<IReadOnlyList<SeriesAccessSummary>> GetAvailableAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var templates = await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => !item.IsAssessment && item.IsActive)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        var progresses = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId && item.IsActive)
            .ToListAsync(cancellationToken);
        var unlockedIds = progresses.Select(item => item.ProgramTemplateId).ToHashSet();
        var hasAnyProgress = progresses.Count > 0;

        return templates
            .Select(item => ToSummary(item, unlockedIds.Contains(item.Id), hasAnyProgress))
            .ToList();
    }

    public async Task<SeriesAccessSummary?> CheckAccessAsync(
        Guid userId,
        Guid seriesId,
        CancellationToken cancellationToken)
    {
        var template = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == seriesId
                && !item.IsAssessment
                && item.IsActive
                , cancellationToken);
        if (template is null)
            return null;

        var isUnlocked = await db.StudentProgramProgresses
            .AsNoTracking()
            .AnyAsync(item => item.ProgramTemplateId == seriesId
                && item.UserId == userId
                , cancellationToken);
        var hasAnyProgress = await db.StudentProgramProgresses
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.IsActive, cancellationToken);
        return ToSummary(template, isUnlocked, hasAnyProgress);
    }

    public async Task<SeriesPrerequisiteSummary?> CheckPrerequisitesAsync(
        Guid userId,
        Guid seriesId,
        CancellationToken cancellationToken)
    {
        var template = await db.ProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == seriesId && !item.IsDeleted, cancellationToken);
        if (template is null)
            return null;

        var hasCompletedSomething = await db.StudentProgramProgresses
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId && item.DaysCompleted > 0, cancellationToken);
        var isMet = template.DisplayOrder <= 1 || hasCompletedSomething;
        return new SeriesPrerequisiteSummary(
            isMet,
            isMet
                ? "Ön koşullar sağlanmış."
                : "Bu seriyi açmak için önce başka bir seri tamamlamanız gerekiyor.",
            null,
            null,
            0,
            hasCompletedSomething ? 100 : 0);
    }

    public async Task<UnlockSeriesResult?> UnlockAsync(
        Guid userId,
        Guid seriesId,
        CancellationToken cancellationToken)
    {
        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == seriesId
                && !item.IsAssessment
                && item.IsActive
                , cancellationToken);
        if (template is null)
            return null;

        var existing = await db.StudentProgramProgresses
            .FirstOrDefaultAsync(item => item.ProgramTemplateId == seriesId
                && item.UserId == userId
                , cancellationToken);
        if (existing is not null)
            return new UnlockSeriesResult(true, "Seri zaten açık.", seriesId);

        var activePrograms = await db.StudentProgramProgresses
            .Where(item => item.UserId == userId && item.IsActive)
            .ToListAsync(cancellationToken);
        var previous = activePrograms.FirstOrDefault();
        var now = DateTime.UtcNow;
        foreach (var activeProgram in activePrograms)
            activeProgram.Deactivate(userId, now);

        db.StudentProgramProgresses.Add(StudentProgramProgress.Start(
            Guid.NewGuid(),
            userId,
            template,
            previous?.CurrentStreak ?? 0,
            previous?.LongestStreak ?? 0,
            userId,
            now));
        await db.SaveChangesAsync(cancellationToken);
        return new UnlockSeriesResult(true, "Seri başarıyla açıldı.", seriesId);
    }

    private static SeriesAccessSummary ToSummary(
        ProgramTemplate template,
        bool isUnlocked,
        bool hasAnyProgress) =>
        new(
            template.Id,
            template.Name,
            template.Name,
            template.Description,
            template.DisplayOrder,
            isUnlocked,
            isUnlocked || template.DisplayOrder <= 1 || hasAnyProgress,
            !isUnlocked && template.DisplayOrder > 1 && !hasAnyProgress
                ? "Bu seriyi açmak için önce başka bir seri tamamlamanız gerekiyor."
                : null,
            null,
            null,
            null,
            null,
            0,
            template.TotalDays,
            template.TotalDays,
            template.InitialDifficultyLevel.ToString(),
            template.MaxDifficultyLevel.ToString());
}
