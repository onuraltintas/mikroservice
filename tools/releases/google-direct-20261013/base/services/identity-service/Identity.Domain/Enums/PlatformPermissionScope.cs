namespace Identity.Domain.Enums;

public static class PlatformPermissionScope
{
    public static bool IsAllowed(string permission, PlatformProduct? product) => product switch
    {
        PlatformProduct.Coaching => !permission.StartsWith("Permissions.SpeedReading.", StringComparison.Ordinal),
        PlatformProduct.SpeedReading => !permission.StartsWith("Permissions.Coaching.", StringComparison.Ordinal),
        _ => true
    };
}
