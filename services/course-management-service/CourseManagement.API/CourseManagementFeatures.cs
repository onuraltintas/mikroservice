namespace CourseManagement.API;

/// <summary>
/// Product switches for the first release (19.11.2026): Kurs Yönetimi opens as an institution panel only.
/// Students and guardians get e-mail, not a portal; homework is recorded by the teacher without files.
/// Both switches are off unless a deployment turns them on; the code behind them stays in place.
/// </summary>
public sealed class CourseManagementFeatures
{
    public const string SectionName = "CourseManagement:Features";

    /// <summary>Student application, student invitations and the participation endpoints.</summary>
    public bool StudentPortal { get; init; }

    /// <summary>Homework file uploads and downloads (needs a virus scanner in production).</summary>
    public bool AssignmentAttachments { get; init; }

    private const string Prefix = "/api/course-management/v1";

    private static readonly string[] StudentPortalPaths =
    [
        Prefix + "/participation",
        Prefix + "/me/courses",
        Prefix + "/me/lessons",
        Prefix + "/me/attendance",
        Prefix + "/me/makeup-credits",
        Prefix + "/me/assignments",
        Prefix + "/me/finance"
    ];

    /// <summary>Whether a request path belongs to a switched-off feature and must answer 404.</summary>
    public bool IsDisabledPath(PathString path)
    {
        var value = path.Value ?? string.Empty;
        if (!StudentPortal && StudentPortalPaths.Any(prefix => StartsWithSegment(value, prefix))) return true;
        if (!AssignmentAttachments && value.StartsWith(Prefix + "/", StringComparison.OrdinalIgnoreCase)
            && value.Contains("/attachments", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static bool StartsWithSegment(string value, string prefix) =>
        value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        && (value.Length == prefix.Length || value[prefix.Length] == '/');
}

public static class CourseManagementFeatureGate
{
    /// <summary>Answers 404 for switched-off features before routing, the same answer as an unknown address.</summary>
    public static IApplicationBuilder UseCourseManagementFeatureGate(this IApplicationBuilder app, CourseManagementFeatures features) =>
        app.Use(async (context, next) =>
        {
            if (features.IsDisabledPath(context.Request.Path))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }
            await next(context);
        });
}
