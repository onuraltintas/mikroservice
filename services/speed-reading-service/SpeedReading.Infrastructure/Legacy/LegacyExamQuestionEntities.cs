namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyExamQuestion : LegacyBaseEntity
{
    public string Content { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string OptionA { get; set; } = string.Empty;
    public string OptionB { get; set; } = string.Empty;
    public string OptionC { get; set; } = string.Empty;
    public string OptionD { get; set; } = string.Empty;
    public string? OptionE { get; set; }
    public string CorrectOption { get; set; } = string.Empty;
    public int ExamType { get; set; }
    public int Difficulty { get; set; }
    public int WordCount { get; set; }
    public string? Topic { get; set; }
    public int Category { get; set; }
    public Guid? TargetAgeGroupConfigurationId { get; set; }
}
