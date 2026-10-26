namespace EduPlatform.Gateway.Configuration;

/// <summary>
/// Course Management is a separate product that production does not run yet. Its routes and
/// cluster stay in the JSON route table (shared with the Course Management overlays), but the
/// gateway only loads them when <c>CourseManagement:Enabled</c> is true, the same switch the
/// Notification service uses. Otherwise the cluster would be probed every few seconds and its
/// paths would answer 502/503 instead of 404.
/// </summary>
public static class CourseManagementRouting
{
    public const string EnabledKey = "CourseManagement:Enabled";
    public const string ClusterId = "course-management-cluster";
    public const string PathPrefix = "/api/course-management";

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue(EnabledKey, false);

    /// <summary>
    /// Returns the ReverseProxy section unchanged when Course Management is enabled; otherwise a
    /// copy without the Course Management cluster and every route that targets it or its paths.
    /// </summary>
    public static IConfiguration ReverseProxySection(IConfiguration configuration)
    {
        var section = configuration.GetSection("ReverseProxy");
        if (IsEnabled(configuration))
        {
            return section;
        }

        var removedRoutes = section.GetSection("Routes").GetChildren()
            .Where(IsCourseManagementRoute)
            .Select(route => $"Routes:{route.Key}:")
            .ToArray();
        var removedCluster = $"Clusters:{ClusterId}:";

        var kept = section.AsEnumerable(makePathsRelative: true)
            .Where(pair => pair.Value is not null)
            .Where(pair => !IsUnder(pair.Key, removedCluster)
                && !removedRoutes.Any(prefix => IsUnder(pair.Key, prefix)));

        return new ConfigurationBuilder().AddInMemoryCollection(kept).Build();
    }

    public static IEnumerable<string> VisibleClusterIds(IConfiguration configuration)
    {
        var clusters = configuration.GetSection("ReverseProxy:Clusters").GetChildren().Select(c => c.Key);
        return IsEnabled(configuration)
            ? clusters
            : clusters.Where(id => !string.Equals(id, ClusterId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCourseManagementRoute(IConfigurationSection route) =>
        string.Equals(route["ClusterId"], ClusterId, StringComparison.OrdinalIgnoreCase)
        || (route["Match:Path"] ?? string.Empty).StartsWith(PathPrefix, StringComparison.OrdinalIgnoreCase);

    private static bool IsUnder(string key, string prefix) =>
        key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key + ":", prefix, StringComparison.OrdinalIgnoreCase);
}
