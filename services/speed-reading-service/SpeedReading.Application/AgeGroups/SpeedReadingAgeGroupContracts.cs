namespace SpeedReading.Application.AgeGroups;

public sealed record AgeGroupSummary(
    Guid Id,
    string Name,
    string DisplayName,
    int MinAge,
    int? MaxAge,
    int RecommendedWPM,
    int MinWPM,
    int MaxWPM,
    int RecommendedComprehension,
    int RecommendedDailyMinutes,
    int DefaultDifficultyLevel,
    int OrderIndex,
    bool IsActive,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record AgeGroupRecommendations(
    int Age,
    int RecommendedWPM,
    int RecommendedComprehension,
    int RecommendedDailyMinutes);

public sealed record CreateAgeGroupRequest(
    string Name,
    string DisplayName,
    int MinAge,
    int? MaxAge,
    int MinWPM,
    int RecommendedWPM,
    int MaxWPM,
    int RecommendedComprehension,
    int RecommendedDailyMinutes,
    int DefaultDifficultyLevel,
    int OrderIndex,
    bool IsActive,
    string? Description);

public sealed record UpdateAgeGroupRequest(
    string Name,
    string DisplayName,
    int MinAge,
    int? MaxAge,
    int MinWPM,
    int RecommendedWPM,
    int MaxWPM,
    int RecommendedComprehension,
    int RecommendedDailyMinutes,
    int DefaultDifficultyLevel,
    int OrderIndex,
    bool IsActive,
    string? Description);

public interface ISpeedReadingAgeGroups
{
    Task<IReadOnlyList<AgeGroupSummary>> GetAllAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<AgeGroupSummary?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<AgeGroupSummary?> GetByAgeAsync(int age, CancellationToken cancellationToken);
    Task<AgeGroupRecommendations?> GetRecommendationsAsync(int age, CancellationToken cancellationToken);
    Task<AgeGroupSummary> CreateAsync(Guid userId, CreateAgeGroupRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateAsync(Guid id, Guid userId, UpdateAgeGroupRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, Guid userId, CancellationToken cancellationToken);
}
