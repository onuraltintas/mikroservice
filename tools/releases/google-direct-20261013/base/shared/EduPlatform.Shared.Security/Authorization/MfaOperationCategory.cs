using Microsoft.AspNetCore.Authorization;

namespace EduPlatform.Shared.Security.Authorization;

/// <summary>
/// Central categories used by the admin MFA policy settings.
/// </summary>
public static class MfaOperationCategories
{
    public const string Users = "users";
    public const string RolesPermissions = "roles-permissions";
    public const string Institutions = "institutions";
    public const string System = "system";
    public const string SpeedReading = "speed-reading";
    public const string Coaching = "coaching";
    public const string Cms = "cms";

    public static IReadOnlyList<string> All { get; } =
    [
        Users,
        RolesPermissions,
        Institutions,
        System,
        SpeedReading,
        Coaching,
        Cms
    ];

    public static bool IsKnown(string? category) =>
        !string.IsNullOrWhiteSpace(category)
        && All.Contains(category.Trim().ToLowerInvariant(), StringComparer.Ordinal);

    public static string Normalize(string category) =>
        category.Trim().ToLowerInvariant();

    public static string ConfigurationKey(string category) =>
        $"security.mfa.{Normalize(category)}";

    public static bool TryGetForPermission(string permission, out string category)
    {
        category = string.Empty;
        if (string.IsNullOrWhiteSpace(permission))
        {
            return false;
        }

        var parts = permission.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3 || !parts[0].Equals("Permissions", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        category = parts[1].ToLowerInvariant() switch
        {
            "users" => Users,
            "roles" or "permissions" => RolesPermissions,
            "institutions" => Institutions,
            "coaching" => Coaching,
            "speedreading" => SpeedReading,
            "support" or "notifications" => Cms,
            "operations" => System,
            _ => string.Empty
        };

        return !string.IsNullOrWhiteSpace(category);
    }

    public static bool TryGetManagementCategory(string permission, out string category)
    {
        if (!TryGetForPermission(permission, out category))
        {
            return false;
        }

        var action = permission[(permission.LastIndexOf('.') + 1)..];
        if (action.Equals("View", StringComparison.OrdinalIgnoreCase)
            || action.Equals("ProgressView", StringComparison.OrdinalIgnoreCase)
            || action.Equals("ReportView", StringComparison.OrdinalIgnoreCase)
            || action.Equals("PlatformAnalyticsView", StringComparison.OrdinalIgnoreCase)
            || action.Equals("LeaderboardView", StringComparison.OrdinalIgnoreCase))
        {
            category = string.Empty;
            return false;
        }

        return true;
    }
}

/// <summary>
/// How the MFA requirement is applied to a category.
/// </summary>
public static class MfaPolicyModes
{
    public const string Required = "required";
    public const string MutationsOnly = "mutations";
    public const string Disabled = "disabled";

    public static bool IsKnown(string? mode) =>
        mode is not null
        && (mode.Equals(Required, StringComparison.OrdinalIgnoreCase)
            || mode.Equals(MutationsOnly, StringComparison.OrdinalIgnoreCase)
            || mode.Equals(Disabled, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Endpoint metadata that assigns an authorization endpoint to an MFA category.
/// The attribute also adds the shared MfaRequired policy to the endpoint.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class MfaCategoryAttribute : AuthorizeAttribute, IMfaCategoryMetadata
{
    public MfaCategoryAttribute(string category)
    {
        if (!MfaOperationCategories.IsKnown(category))
        {
            throw new ArgumentException($"Unknown MFA operation category: {category}", nameof(category));
        }

        Category = MfaOperationCategories.Normalize(category);
        Policy = "MfaRequired";
    }

    public string Category { get; }
}

public interface IMfaCategoryMetadata
{
    string? Category { get; }
}
