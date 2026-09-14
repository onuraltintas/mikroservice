namespace SpeedReading.Application.Analytics;

public static class TeacherStudentAccessRules
{
    public static bool ContainsAll(
        IEnumerable<Guid>? requestedStudentIds,
        IEnumerable<Guid>? authorizedStudentIds)
    {
        var authorized = (authorizedStudentIds ?? [])
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        return (requestedStudentIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .All(authorized.Contains);
    }
}
