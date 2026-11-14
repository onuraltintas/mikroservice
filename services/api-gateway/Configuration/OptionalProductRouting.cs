namespace EduPlatform.Gateway.Configuration;

/// <summary>
/// Products that an environment may run without. Their routes and cluster stay in the JSON route table, but the
/// gateway only loads them when the product's switch is true. Otherwise the cluster would be probed every few
/// seconds and its paths would answer 502/503 instead of 404.
/// </summary>
public static class OptionalProductRouting
{
    public sealed record Product(string EnabledKey, string ClusterId, string PathPrefix);

    /// <summary>
    /// Billing ("Abonelik ve Ödeme") is built in stages and goes live in one release at the end (decision
    /// 14.11.2026); until its overlay is used, no environment routes to it.
    /// </summary>
    public static readonly Product Billing = new("Billing:Enabled", "billing-cluster", "/api/billing");

    public static readonly Product CourseManagement =
        new(CourseManagementRouting.EnabledKey, CourseManagementRouting.ClusterId, CourseManagementRouting.PathPrefix);

    public static readonly IReadOnlyList<Product> Products = [CourseManagement, Billing];

    public static bool IsEnabled(IConfiguration configuration, Product product) =>
        configuration.GetValue(product.EnabledKey, false);

    /// <summary>
    /// Returns the ReverseProxy section unchanged when every optional product is enabled; otherwise a copy without
    /// the disabled products' clusters and every route that targets them or their paths.
    /// </summary>
    public static IConfiguration ReverseProxySection(IConfiguration configuration)
    {
        var section = configuration.GetSection("ReverseProxy");
        var disabled = Products.Where(product => !IsEnabled(configuration, product)).ToArray();
        if (disabled.Length == 0)
        {
            return section;
        }

        var removed = section.GetSection("Routes").GetChildren()
            .Where(route => disabled.Any(product => IsRouteOf(route, product)))
            .Select(route => $"Routes:{route.Key}:")
            .Concat(disabled.Select(product => $"Clusters:{product.ClusterId}:"))
            .ToArray();

        var kept = section.AsEnumerable(makePathsRelative: true)
            .Where(pair => pair.Value is not null)
            .Where(pair => !removed.Any(prefix => IsUnder(pair.Key, prefix)));

        return new ConfigurationBuilder().AddInMemoryCollection(kept).Build();
    }

    public static IEnumerable<string> VisibleClusterIds(IConfiguration configuration)
    {
        var disabled = Products.Where(product => !IsEnabled(configuration, product)).Select(product => product.ClusterId).ToArray();
        return configuration.GetSection("ReverseProxy:Clusters").GetChildren().Select(cluster => cluster.Key)
            .Where(id => !disabled.Contains(id, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsRouteOf(IConfigurationSection route, Product product) =>
        string.Equals(route["ClusterId"], product.ClusterId, StringComparison.OrdinalIgnoreCase)
        || IsPathUnder(route["Match:Path"] ?? string.Empty, product.PathPrefix);

    // "/api/billing" and "/api/billing/…", but not "/api/billing-something".
    private static bool IsPathUnder(string path, string prefix) =>
        path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && (path.Length == prefix.Length || path[prefix.Length] == '/');

    private static bool IsUnder(string key, string prefix) =>
        key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        || string.Equals(key + ":", prefix, StringComparison.OrdinalIgnoreCase);
}
