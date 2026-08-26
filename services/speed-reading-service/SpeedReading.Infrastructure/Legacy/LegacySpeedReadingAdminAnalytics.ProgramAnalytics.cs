using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed partial class LegacySpeedReadingAdminAnalytics
{
    public async Task<SpeedReadingProgramAnalytics> GetProgramAnalyticsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
            from progress in db.StudentProgramProgresses.AsNoTracking()
            join user in db.Users.AsNoTracking()
                on progress.UserId equals user.Id into userRows
            from user in userRows.DefaultIfEmpty()
            join template in db.ExerciseProgramTemplates.AsNoTracking()
                on progress.ProgramTemplateId equals template.Id into templateRows
            from template in templateRows.DefaultIfEmpty()
            where !progress.IsDeleted && progress.IsActive
            select new SpeedReadingProgramAnalyticsRow(
                progress.UserId,
                user == null ? string.Empty : user.FirstName,
                user == null ? string.Empty : user.LastName,
                user == null ? null : user.Email,
                progress.ProgramTemplateId,
                template == null ? string.Empty : template.Name,
                progress.CurrentWeek,
                progress.CurrentDay,
                progress.CurrentStreak,
                progress.LongestStreak,
                progress.AverageSuccessRate,
                progress.CurrentDifficultyLevel,
                progress.UpdatedAt ?? progress.CreatedAt,
                progress.DaysCompleted,
                progress.ExercisesCompleted,
                progress.IsActive))
            .ToListAsync(cancellationToken);

        return SpeedReadingProgramAnalyticsCalculator.Calculate(rows);
    }
}
