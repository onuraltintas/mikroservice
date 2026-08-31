using Identity.Domain.Enums;

namespace Identity.Application.DTOs.Institutions;

public sealed record InstitutionDto(
    Guid Id,
    string Name,
    InstitutionType Type,
    string? LogoUrl,
    string? Address,
    string? City,
    string? District,
    string? Phone,
    string? Email,
    string? Website,
    LicenseType LicenseType,
    int MaxStudents,
    int MaxTeachers,
    DateTime? SubscriptionStartDate,
    DateTime? SubscriptionEndDate,
    bool IsActive,
    int StudentCount,
    int TeacherCount,
    int AdminCount,
    DateTime CreatedAt);

public sealed record AssignInstitutionAdminRequest(Guid UserId, InstitutionAdminRole Role);

public sealed record InstitutionAdminDto(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    InstitutionAdminRole Role,
    bool IsActive);
