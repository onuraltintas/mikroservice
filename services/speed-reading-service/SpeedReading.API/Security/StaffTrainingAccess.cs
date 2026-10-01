using System.Security.Claims;

namespace SpeedReading.API.Security;

public static class StaffTrainingAccess
{
    public static bool IsAllowed(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true
        && !user.IsInRole("Student")
        && (user.IsInRole("Admin") || user.IsInRole("SystemAdmin") || user.IsInRole("Teacher"));
}
