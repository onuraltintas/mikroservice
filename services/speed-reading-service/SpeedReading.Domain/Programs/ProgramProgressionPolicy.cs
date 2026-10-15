namespace SpeedReading.Domain.Programs;

public sealed class ProgramProgressionPolicy
{
    // Guid.Empty is the service default; real student identifiers must be non-empty.
    public Guid UserId { get; set; }
    public bool? AllowEarlyAdvance { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
}
