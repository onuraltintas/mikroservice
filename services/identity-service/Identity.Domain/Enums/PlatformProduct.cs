namespace Identity.Domain.Enums;

public enum PlatformProduct
{
    Coaching = 1,
    SpeedReading = 2,
    CourseManagement = 3
}

public enum UserProductAccessSource
{
    SelfRegistration = 1,
    GoogleRegistration = 2,
    Admin = 3,
    Invitation = 4,
    Migration = 5
}

public static class PlatformProductExtensions
{
    public static string ToRouteValue(this PlatformProduct product) => product switch
    {
        PlatformProduct.Coaching => "coaching",
        PlatformProduct.SpeedReading => "speed-reading",
        PlatformProduct.CourseManagement => "course-management",
        _ => throw new ArgumentOutOfRangeException(nameof(product))
    };

    public static bool TryParseRouteValue(string? value, out PlatformProduct product)
    {
        switch (value?.ToLowerInvariant())
        {
            case "coaching":
                product = PlatformProduct.Coaching;
                return true;
            case "speed-reading":
                product = PlatformProduct.SpeedReading;
                return true;
            case "course-management":
                product = PlatformProduct.CourseManagement;
                return true;
            default:
                product = default;
                return false;
        }
    }
}
