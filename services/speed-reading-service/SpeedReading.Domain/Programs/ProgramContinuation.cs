namespace SpeedReading.Domain.Programs;

public sealed class ProgramContinuation
{
    private ProgramContinuation() { }
    public Guid SourceProgressId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid AssessmentAttemptId { get; private set; }
    public Guid TemplateId { get; private set; }
    public int RecommendedLevel { get; private set; }
    public int MeasuredLevel { get; private set; }
    public string PolicyVersion { get; private set; } = string.Empty;
    public bool OverrideAcknowledged { get; private set; }
    public DateTime RequestedAt { get; private set; }
    public Guid? ConfirmedBy { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public Guid? NewProgressId { get; private set; }

    public static ProgramContinuation Request(Guid source, Guid student, Guid assessment, Guid template,
        int recommendedLevel, int measuredLevel, string policyVersion, bool acknowledged, DateTime now) => new()
    {
        SourceProgressId = source, StudentId = student, AssessmentAttemptId = assessment, TemplateId = template,
        RecommendedLevel = recommendedLevel, MeasuredLevel = measuredLevel, PolicyVersion = policyVersion,
        OverrideAcknowledged = acknowledged, RequestedAt = now
    };

    public void Confirm(Guid actor, Guid progress, DateTime now)
    {
        if (NewProgressId.HasValue) throw new InvalidOperationException("Geçiş zaten onaylandı.");
        ConfirmedBy = actor; NewProgressId = progress; ConfirmedAt = now;
    }

    public void ChangeSelection(Guid assessment, Guid template, int recommendedLevel, int measuredLevel,
        string policyVersion, bool acknowledged, DateTime now)
    {
        if (ConfirmedAt.HasValue) throw new InvalidOperationException("Geçiş zaten onaylandı.");
        AssessmentAttemptId = assessment; TemplateId = template; RecommendedLevel = recommendedLevel;
        MeasuredLevel = measuredLevel; PolicyVersion = policyVersion; OverrideAcknowledged = acknowledged; RequestedAt = now;
    }
}
