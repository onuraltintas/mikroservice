namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyExerciseReviewItem : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid? ProgramTemplateId { get; set; }
    public DateTime NextReviewDate { get; set; }
    public int ReviewCount { get; set; }
    public int IntervalDays { get; set; }
    public double EasinessFactor { get; set; }
    public bool IsMastered { get; set; }
    public double? LastScore { get; set; }
}
