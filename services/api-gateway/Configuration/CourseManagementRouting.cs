namespace EduPlatform.Gateway.Configuration;

/// <summary>
/// Course Management is a separate product that production does not run yet. Its routes and
/// cluster stay in the JSON route table (shared with the Course Management overlays), but the
/// gateway only loads them when <c>CourseManagement:Enabled</c> is true, the same switch the
/// Notification service uses. Otherwise the cluster would be probed every few seconds and its
/// paths would answer 502/503 instead of 404. The filtering itself is shared with the other optional
/// products: see <see cref="OptionalProductRouting"/>.
/// </summary>
public static class CourseManagementRouting
{
    public const string EnabledKey = "CourseManagement:Enabled";
    public const string ClusterId = "course-management-cluster";
    public const string PathPrefix = "/api/course-management";

    public static bool IsEnabled(IConfiguration configuration) =>
        configuration.GetValue(EnabledKey, false);
}
