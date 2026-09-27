using SpeedReading.Application.ExerciseSessions;

namespace SpeedReading.Application.UnitTests;

public sealed class ExercisePreviewRulesTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SystemAdmin", true)]
    [InlineData("Editor", true)]
    [InlineData("Student", false)]
    [InlineData("Teacher", false)]
    public void OnlyAdministratorsAndEditorsAreForcedIntoPreview(string role, bool expected)
    {
        Assert.Equal(expected, ExercisePreviewRules.IsPreviewOnlyRole([role]));
    }

    [Theory]
    [InlineData("/api/speed-reading/exercise-sessions/start", "POST", true)]
    [InlineData("/api/speed-reading/exercise-sessions/00000000-0000-0000-0000-000000000001/complete", "POST", true)]
    [InlineData("/api/speed-reading/daily-progress/complete-exercise", "POST", true)]
    [InlineData("/api/speed-reading/exercises", "POST", false)]
    [InlineData("/api/speed-reading/exercises", "GET", false)]
    [InlineData("/api/speed-reading/exercise-sessions/active", "GET", false)]
    public void PreviewRolesCannotPersistExerciseResults(string path, string method, bool expected)
    {
        Assert.Equal(expected, ExercisePreviewRules.BlocksPersistentResult(path, method));
    }
}
