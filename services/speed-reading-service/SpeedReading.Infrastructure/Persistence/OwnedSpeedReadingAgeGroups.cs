using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.AgeGroups;
using SpeedReading.Domain.AgeGroups;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingAgeGroups(OwnedSpeedReadingDbContext db) : ISpeedReadingAgeGroups
{
    public async Task<IReadOnlyList<AgeGroupSummary>> GetAllAsync(
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        var query = db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        if (activeOnly)
            query = query.Where(item => item.IsActive);

        return await query
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.MinAge)
            .Select(ToSummary())
            .ToListAsync(cancellationToken);
    }

    public async Task<AgeGroupSummary?> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await db.AgeGroupConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id && !row.IsDeleted, cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    public async Task<AgeGroupSummary?> GetByAgeAsync(
        int age,
        CancellationToken cancellationToken)
    {
        ValidateAge(age);
        var item = await FindForAgeAsync(age, cancellationToken);
        return item is null ? null : ToSummary(item);
    }

    public async Task<AgeGroupRecommendations?> GetRecommendationsAsync(
        int age,
        CancellationToken cancellationToken)
    {
        ValidateAge(age);
        var item = await FindForAgeAsync(age, cancellationToken);
        return item is null
            ? null
            : new AgeGroupRecommendations(
                age,
                item.RecommendedWPM,
                item.RecommendedComprehension,
                item.RecommendedDailyMinutes);
    }

    public async Task<AgeGroupSummary> CreateAsync(
        Guid userId,
        CreateAgeGroupRequest request,
        CancellationToken cancellationToken)
    {
        var item = AgeGroupConfiguration.Create(
            Guid.NewGuid(),
            request.Name,
            request.DisplayName,
            request.MinAge,
            request.MaxAge,
            request.MinWPM,
            request.RecommendedWPM,
            request.MaxWPM,
            request.RecommendedComprehension,
            request.RecommendedDailyMinutes,
            request.DefaultDifficultyLevel,
            request.OrderIndex,
            request.IsActive,
            request.Description,
            userId,
            DateTime.UtcNow);
        db.AgeGroupConfigurations.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(item);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateAgeGroupRequest request,
        CancellationToken cancellationToken)
    {
        var item = await db.AgeGroupConfigurations
            .SingleOrDefaultAsync(row => row.Id == id && !row.IsDeleted, cancellationToken);
        if (item is null)
            return false;

        item.Update(
            request.Name,
            request.DisplayName,
            request.MinAge,
            request.MaxAge,
            request.MinWPM,
            request.RecommendedWPM,
            request.MaxWPM,
            request.RecommendedComprehension,
            request.RecommendedDailyMinutes,
            request.DefaultDifficultyLevel,
            request.OrderIndex,
            request.IsActive,
            request.Description,
            userId,
            DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var item = await db.AgeGroupConfigurations
            .SingleOrDefaultAsync(row => row.Id == id && !row.IsDeleted, cancellationToken);
        if (item is null)
            return false;

        item.Delete(userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AgeGroupConfiguration?> FindForAgeAsync(
        int age,
        CancellationToken cancellationToken) =>
        await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => item.IsActive
                && !item.IsDeleted
                && item.MinAge <= age
                && (!item.MaxAge.HasValue || item.MaxAge.Value >= age))
            .OrderBy(item => item.MinAge)
            .FirstOrDefaultAsync(cancellationToken);

    private static System.Linq.Expressions.Expression<Func<AgeGroupConfiguration, AgeGroupSummary>> ToSummary() =>
        item => new AgeGroupSummary(
            item.Id,
            item.Name,
            item.DisplayName,
            item.MinAge,
            item.MaxAge,
            item.RecommendedWPM,
            item.MinWPM,
            item.MaxWPM,
            item.RecommendedComprehension,
            item.RecommendedDailyMinutes,
            item.DefaultDifficultyLevel,
            item.OrderIndex,
            item.IsActive,
            item.Description,
            item.CreatedAt,
            item.UpdatedAt);

    private static AgeGroupSummary ToSummary(AgeGroupConfiguration item) =>
        new(
            item.Id,
            item.Name,
            item.DisplayName,
            item.MinAge,
            item.MaxAge,
            item.RecommendedWPM,
            item.MinWPM,
            item.MaxWPM,
            item.RecommendedComprehension,
            item.RecommendedDailyMinutes,
            item.DefaultDifficultyLevel,
            item.OrderIndex,
            item.IsActive,
            item.Description,
            item.CreatedAt,
            item.UpdatedAt);

    private static void ValidateAge(int age)
    {
        if (age is < 0 or > 150)
            throw new ArgumentException("Age must be between 0 and 150.", nameof(age));
    }
}
