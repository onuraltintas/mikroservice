namespace SpeedReading.Domain.Assessment;

public static class AssessmentAttemptPhaseRules
{
    public static bool TryGetPrerequisite(
        AssessmentAttemptPhase phase,
        out AssessmentAttemptPhase prerequisite)
    {
        switch (phase)
        {
            case AssessmentAttemptPhase.PostTraining:
                prerequisite = AssessmentAttemptPhase.Baseline;
                return true;
            case AssessmentAttemptPhase.Retention:
                prerequisite = AssessmentAttemptPhase.PostTraining;
                return true;
            case AssessmentAttemptPhase.Transfer:
                prerequisite = AssessmentAttemptPhase.Retention;
                return true;
            default:
                prerequisite = default;
                return false;
        }
    }
}
