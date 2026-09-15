namespace Identity.Application.Authorization;

public static class TeacherStudentScopeRules
{
    public static bool ShouldUseInstitutionScope(
        IEnumerable<string> roles,
        Guid? targetTeacherUserId) =>
        !targetTeacherUserId.HasValue
        && roles.Any(role => string.Equals(role, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "InstitutionOwner", StringComparison.OrdinalIgnoreCase));
}
