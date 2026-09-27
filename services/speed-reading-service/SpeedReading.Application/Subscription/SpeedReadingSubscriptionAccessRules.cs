namespace SpeedReading.Application.Subscription;

public static class SpeedReadingSubscriptionAccessRules
{
    private static readonly HashSet<string> FreeSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "assessment",
        "subscriptions",
        "subscription-plans",
        "products",
        "payment",
        "bank-transfer",
        "cms"
    };

    private static readonly HashSet<string> AssessmentCatalogSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "exercises",
        "exercise-types",
        "reading-texts",
        "reading-questions",
        "age-group-configurations"
    };

    public static bool RequiresSubscription(string path, string method)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3
            || !parts[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            || !parts[1].Equals("speed-reading", StringComparison.OrdinalIgnoreCase))
            return false;

        var section = parts[2];
        if (FreeSections.Contains(section))
            return false;
        if (section.Equals("adaptive-learning", StringComparison.OrdinalIgnoreCase)
            && parts.Length == 4
            && parts[3].Equals("profile", StringComparison.OrdinalIgnoreCase)
            && (method.Equals("GET", StringComparison.OrdinalIgnoreCase)
                || method.Equals("PUT", StringComparison.OrdinalIgnoreCase)))
            return false;
        if (section.Equals("adaptive-learning", StringComparison.OrdinalIgnoreCase)
            && parts.Length == 5
            && parts[3].Equals("profile", StringComparison.OrdinalIgnoreCase)
            && parts[4].Equals("status", StringComparison.OrdinalIgnoreCase)
            && method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            return false;
        if (method.Equals("GET", StringComparison.OrdinalIgnoreCase)
            && AssessmentCatalogSections.Contains(section))
            return false;
        if (section.Equals("sitemap.xml", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
