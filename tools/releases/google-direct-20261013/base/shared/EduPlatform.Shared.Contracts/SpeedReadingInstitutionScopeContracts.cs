namespace EduPlatform.Shared.Contracts.Reporting;

/// <summary>
/// Identity-owned institution directory metadata consumed by Speed Reading.
/// User counts, memberships, activity and performance remain in the
/// Speed Reading database.
/// </summary>
public sealed record SpeedReadingInstitutionScopeItem(
    Guid InstitutionId,
    string InstitutionName,
    bool IsActive);

public sealed record SpeedReadingInstitutionScopeResponse(
    IReadOnlyList<SpeedReadingInstitutionScopeItem> Institutions);

public sealed record SpeedReadingInstitutionManagerAuthorizationRequest(
    Guid UserId,
    Guid InstitutionId);

public sealed record SpeedReadingInstitutionManagerAuthorizationResponse(
    bool CanManage);
