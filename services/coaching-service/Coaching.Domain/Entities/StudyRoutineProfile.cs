using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>
/// H3 (user decision K2): the student's answers to the "günlük düzen" questions. The answers produce her study availability
/// (which stays the single source of free hours); AvailabilityVersion is the availability version this profile wrote, so a later
/// hand edit of the free hours can be told apart.
/// </summary>
public sealed class StudyRoutineProfile : AggregateRoot
{
    public const int CurrentSchemaVersion = 1;
    public Guid StudentId { get; private set; }
    public int SchemaVersion { get; private set; } = CurrentSchemaVersion;
    public string AnswersJson { get; private set; } = "{}";
    public int AvailabilityVersion { get; private set; }
    private StudyRoutineProfile() { }

    public static StudyRoutineProfile Create(Guid studentId)
    {
        if (studentId == Guid.Empty) throw new ArgumentException("Student ownership is required.", nameof(studentId));
        return new StudyRoutineProfile { StudentId = studentId, UpdatedAt = DateTime.UtcNow };
    }

    public void Replace(string answersJson, int availabilityVersion)
    {
        if (string.IsNullOrWhiteSpace(answersJson) || !answersJson.TrimStart().StartsWith('{'))
            throw new ArgumentException("Routine answers must be a JSON object.", nameof(answersJson));
        ArgumentOutOfRangeException.ThrowIfNegative(availabilityVersion);
        SchemaVersion = CurrentSchemaVersion;
        AnswersJson = answersJson;
        AvailabilityVersion = availabilityVersion;
        UpdatedAt = DateTime.UtcNow;
    }
}
