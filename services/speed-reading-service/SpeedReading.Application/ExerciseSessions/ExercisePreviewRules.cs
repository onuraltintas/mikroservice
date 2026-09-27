namespace SpeedReading.Application.ExerciseSessions;

public static class ExercisePreviewRules
{
    public static bool IsPreviewOnlyRole(IEnumerable<string> roles) =>
        roles.Any(role => role is "Admin" or "SystemAdmin" or "Editor");

    public static bool BlocksPersistentResult(string path, string method)
    {
        if (!method.Equals("POST", StringComparison.OrdinalIgnoreCase)) return false;
        return path.StartsWith("/api/speed-reading/exercise-sessions/", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/speed-reading/daily-progress/complete-exercise", StringComparison.OrdinalIgnoreCase);
    }
}
