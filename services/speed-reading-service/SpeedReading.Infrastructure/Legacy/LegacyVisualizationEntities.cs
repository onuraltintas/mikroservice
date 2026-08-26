namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyVisualizationScene : LegacyBaseEntity
{
    public Guid ExerciseId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Duration { get; set; }
    public int DisplayOrder { get; set; }
    public int DifficultyLevel { get; set; }
    public Guid? TargetAgeGroupConfigurationId { get; set; }
}

internal sealed class LegacyVisualizationQuestion : LegacyBaseEntity
{
    public Guid SceneId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string OptionsJson { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "detail";
    public int DisplayOrder { get; set; }
    public string? HintText { get; set; }
}
