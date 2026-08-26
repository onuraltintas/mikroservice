namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyAssignment : LegacyBaseEntity
{
    public Guid TeacherId { get; set; }
    public Guid ExerciseId { get; set; }
    public Guid? ReadingTextId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public bool IsActive { get; set; }
}

internal sealed class LegacyStudentAssignment : LegacyBaseEntity
{
    public Guid AssignmentId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletionDate { get; set; }
    public Guid? ResultId { get; set; }
    public decimal? Score { get; set; }
    public decimal? KeyPerformanceMetric { get; set; }
}
