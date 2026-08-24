namespace EduPlatform.Shared.Contracts.Reporting;

/// <summary>
/// Identity-owned institution directory data consumed by the speed-reading
/// reporting boundary. Activity and performance metrics remain in the
/// speed-reading database; Identity is the source of truth for tenant names,
/// lifecycle and role counts.
/// </summary>
public sealed record SpeedReadingInstitutionScopeItem(
    Guid InstitutionId,
    string InstitutionName,
    bool IsActive,
    int TotalStudents,
    int TotalTeachers,
    int TotalAdmins);

public sealed record SpeedReadingInstitutionScopeResponse(
    IReadOnlyList<SpeedReadingInstitutionScopeItem> Institutions);
