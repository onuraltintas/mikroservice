namespace Coaching.Application.Subscriptions;

public static class CoachingSubscriptionAccessRules
{
    private static readonly HashSet<string> FreeSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "cms",
        "subscription-plans",
        "subscriptions",
        "bank-transfer",
        "payment",
        "finance"
    };

    private static readonly HashSet<string> LearningSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "assignments", "exams", "goals", "sessions", "reports", "calendar"
    };

    public static bool RequiresSubscription(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            && parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && (LearningSections.Contains(parts[1])
                || parts.Length >= 3 && parts[1].Equals("coaching", StringComparison.OrdinalIgnoreCase)
                    && !FreeSections.Contains(parts[2]));
    }

    public static bool HasAccess(
        bool enforcementEnabled,
        bool isStudent,
        bool isStaff,
        bool hasActiveSubscription) =>
        !enforcementEnabled || !isStudent || isStaff || hasActiveSubscription;
}
