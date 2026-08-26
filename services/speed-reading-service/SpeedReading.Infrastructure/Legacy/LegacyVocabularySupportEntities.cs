namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyAgeGroupConfiguration : LegacyBaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int RecommendedWPM { get; set; }
    public int MinWPM { get; set; }
    public int MaxWPM { get; set; }
    public int RecommendedComprehension { get; set; }
    public int RecommendedDailyMinutes { get; set; }
    public int DefaultDifficultyLevel { get; set; }
    public int OrderIndex { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}
