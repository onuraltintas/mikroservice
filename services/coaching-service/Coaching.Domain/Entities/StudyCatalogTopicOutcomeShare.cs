namespace Coaching.Domain.Entities;

// A portion belongs to one reviewed duration version. Existing tasks retain
// their original estimate and allocated minutes when a new version is published.
public sealed class StudyCatalogTopicOutcomeShare
{
    public Guid TopicId { get; private set; }
    public Guid OutcomeId { get; private set; }
    public Guid EstimateId { get; private set; }
    public int LearningMinutes { get; private set; }
    public int PracticeMinutes { get; private set; }
    public int CheckMinutes { get; private set; }
    public int TotalMinutes => LearningMinutes + PracticeMinutes + CheckMinutes;
    public string ScopeDescription { get; private set; } = "";
    private StudyCatalogTopicOutcomeShare() { }

    public static StudyCatalogTopicOutcomeShare Create(Guid topic, OutcomeDurationEstimate estimate,
        int learning, int practice, int check, string scope)
    {
        ArgumentNullException.ThrowIfNull(estimate);
        if (topic == Guid.Empty || learning < 0 || practice < 0 || check < 0
            || (long)learning + practice + check is < 1 or > 1440)
            throw new ArgumentException("Konu kapsamının süre paylarını kontrol edin.");
        return new() { TopicId = topic, OutcomeId = estimate.OutcomeId, EstimateId = estimate.Id,
            LearningMinutes = learning, PracticeMinutes = practice, CheckMinutes = check,
            ScopeDescription = StudyCatalogText.Require(scope, 2000) };
    }
}
