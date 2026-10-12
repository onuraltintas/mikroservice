namespace Identity.Domain.Enums;

public static class PlatformPermissionScope
{
    public static bool IsAllowed(string permission, PlatformProduct? product) => product switch
    {
        PlatformProduct.Coaching => !permission.StartsWith("Permissions.SpeedReading.", StringComparison.Ordinal)
            && !permission.StartsWith("Permissions.CourseManagement.", StringComparison.Ordinal),
        PlatformProduct.SpeedReading => !permission.StartsWith("Permissions.Coaching.", StringComparison.Ordinal)
            && !permission.StartsWith("Permissions.CourseManagement.", StringComparison.Ordinal),
        PlatformProduct.CourseManagement => !permission.StartsWith("Permissions.Coaching.", StringComparison.Ordinal)
            && !permission.StartsWith("Permissions.SpeedReading.", StringComparison.Ordinal),
        _ => true
    };
}
