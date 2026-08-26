using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.AgeGroups;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAgeGroups(SpeedReadingDbContext db) : ISpeedReadingAgeGroups
{
    public async Task<IReadOnlyList<AgeGroupSummary>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var query = db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        if (activeOnly) query = query.Where(item => item.IsActive);

        var rows = await query
            .OrderBy(item => item.OrderIndex)
            .ThenBy(item => item.MinAge)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<AgeGroupSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.AgeGroupConfigurations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<AgeGroupSummary?> GetByAgeAsync(int age, CancellationToken cancellationToken)
    {
        ValidateAge(age);
        var row = await FindForAge(age, cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<AgeGroupRecommendations?> GetRecommendationsAsync(int age, CancellationToken cancellationToken)
    {
        ValidateAge(age);
        var row = await FindForAge(age, cancellationToken);
        return row is null
            ? null
            : new AgeGroupRecommendations(
                age,
                row.RecommendedWPM,
                row.RecommendedComprehension,
                row.RecommendedDailyMinutes);
    }

    public async Task<AgeGroupSummary> CreateAsync(
        Guid userId,
        CreateAgeGroupRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.DisplayName, request.MinAge, request.MaxAge, request.MinWPM,
            request.RecommendedWPM, request.MaxWPM, request.RecommendedComprehension,
            request.RecommendedDailyMinutes, request.DefaultDifficultyLevel);

        var row = new LegacyAgeGroupConfiguration
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName.Trim(),
            MinAge = request.MinAge,
            MaxAge = request.MaxAge,
            MinWPM = request.MinWPM,
            RecommendedWPM = request.RecommendedWPM,
            MaxWPM = request.MaxWPM,
            RecommendedComprehension = request.RecommendedComprehension,
            RecommendedDailyMinutes = request.RecommendedDailyMinutes,
            DefaultDifficultyLevel = request.DefaultDifficultyLevel,
            OrderIndex = request.OrderIndex,
            IsActive = request.IsActive,
            Description = Normalize(request.Description),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        };

        db.AgeGroupConfigurations.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<bool> UpdateAsync(
        Guid id,
        Guid userId,
        UpdateAgeGroupRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.DisplayName, request.MinAge, request.MaxAge, request.MinWPM,
            request.RecommendedWPM, request.MaxWPM, request.RecommendedComprehension,
            request.RecommendedDailyMinutes, request.DefaultDifficultyLevel);

        var row = await db.AgeGroupConfigurations
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;

        row.Name = request.Name.Trim();
        row.DisplayName = request.DisplayName.Trim();
        row.MinAge = request.MinAge;
        row.MaxAge = request.MaxAge;
        row.MinWPM = request.MinWPM;
        row.RecommendedWPM = request.RecommendedWPM;
        row.MaxWPM = request.MaxWPM;
        row.RecommendedComprehension = request.RecommendedComprehension;
        row.RecommendedDailyMinutes = request.RecommendedDailyMinutes;
        row.DefaultDifficultyLevel = request.DefaultDifficultyLevel;
        row.OrderIndex = request.OrderIndex;
        row.IsActive = request.IsActive;
        row.Description = Normalize(request.Description);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var row = await db.AgeGroupConfigurations
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;

        row.IsDeleted = true;
        row.DeletedAt = DateTime.UtcNow;
        row.DeletedBy = userId;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<LegacyAgeGroupConfiguration?> FindForAge(int age, CancellationToken cancellationToken) =>
        await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted
                && item.MinAge <= age
                && (!item.MaxAge.HasValue || item.MaxAge.Value >= age))
            .OrderBy(item => item.MinAge)
            .FirstOrDefaultAsync(cancellationToken);

    private static AgeGroupSummary ToSummary(LegacyAgeGroupConfiguration row) =>
        new(
            row.Id,
            row.Name,
            row.DisplayName,
            row.MinAge,
            row.MaxAge,
            row.RecommendedWPM,
            row.MinWPM,
            row.MaxWPM,
            row.RecommendedComprehension,
            row.RecommendedDailyMinutes,
            row.DefaultDifficultyLevel,
            row.OrderIndex,
            row.IsActive,
            row.Description,
            row.CreatedAt,
            row.UpdatedAt);

    private static void Validate(
        string name,
        string displayName,
        int minAge,
        int? maxAge,
        int minWpm,
        int recommendedWpm,
        int maxWpm,
        int recommendedComprehension,
        int recommendedDailyMinutes,
        int defaultDifficultyLevel)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            throw new ArgumentException("Name is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 200)
            throw new ArgumentException("DisplayName is required and must not exceed 200 characters.");
        ValidateAge(minAge);
        if (maxAge.HasValue && maxAge.Value < minAge)
            throw new ArgumentException("MaxAge cannot be lower than MinAge.");
        if (minWpm < 0 || recommendedWpm < minWpm || maxWpm < recommendedWpm)
            throw new ArgumentException("WPM values must satisfy MinWPM <= RecommendedWPM <= MaxWPM.");
        if (recommendedComprehension is < 0 or > 100)
            throw new ArgumentException("RecommendedComprehension must be between 0 and 100.");
        if (recommendedDailyMinutes is < 0 or > 1440)
            throw new ArgumentException("RecommendedDailyMinutes must be between 0 and 1440.");
        if (defaultDifficultyLevel is < 1 or > 5)
            throw new ArgumentException("DefaultDifficultyLevel must be between 1 and 5.");
    }

    private static void ValidateAge(int age)
    {
        if (age is < 0 or > 150) throw new ArgumentException("Age must be between 0 and 150.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
