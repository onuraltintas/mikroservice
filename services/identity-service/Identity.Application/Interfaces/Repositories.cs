using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Entities;

using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetUserProfile;
using Identity.Application.DTOs.Institutions;
using Identity.Domain.Enums;

namespace Identity.Application.Interfaces;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<User?> GetByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken);
    Task<bool> TryAddLoginAsync(User user, UserLogin login, CancellationToken cancellationToken);
    void Delete(User user);
    Task<PagedList<UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken);
    Task<UserSummaryDto> GetSummaryAsync(Guid? institutionId, CancellationToken cancellationToken);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, CancellationToken cancellationToken);
    Task RevokeActiveRefreshTokensForInstitutionAsync(Guid institutionId, string reason, CancellationToken cancellationToken);
    Task<List<User>> GetUsersByRolesAsync(List<string> roleNames, CancellationToken cancellationToken);
}

public interface IIdempotencyRepository
{
    Task<IdempotencyRecord?> GetAsync(
        string scope,
        string key,
        CancellationToken cancellationToken);

    Task AddAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}

public interface IRoleRepository
{
    Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken);
    Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken);
    Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Role role, CancellationToken cancellationToken);
    void Delete(Role role);
    void AddRolePermission(RolePermission permission);
    void RemoveRolePermission(RolePermission permission);
    Task RemovePermissionFromAllRolesAsync(string permissionKey, CancellationToken cancellationToken);
}

public interface IInstitutionRepository
{
    Task AddAsync(Institution institution, CancellationToken cancellationToken);
    Task AddAdminAsync(InstitutionAdmin admin, CancellationToken cancellationToken);
    Task<PagedList<InstitutionDto>> GetAllAsync(int page, int pageSize, string? searchTerm, bool? isActive, Guid? institutionId, CancellationToken cancellationToken);
    Task<InstitutionDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Institution?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> HasAdminAsync(Guid institutionId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<InstitutionAdminDto>> GetAdminsAsync(Guid institutionId, CancellationToken cancellationToken);
    Task<InstitutionAdmin?> GetAdminAsync(Guid institutionId, Guid userId, CancellationToken cancellationToken);
    Task<Guid?> GetInstitutionIdByAdminIdAsync(Guid adminUserId, CancellationToken cancellationToken);
    Task<Guid?> GetPrimaryInstitutionIdByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> IsUserInInstitutionAsync(Guid userId, Guid institutionId, CancellationToken cancellationToken);
    Task<CoachingTeacherAuthorization?> AuthorizeCoachingTeacherTargetsAsync(
        Guid teacherUserId,
        IReadOnlyCollection<Guid> studentUserIds,
        Guid? requestedInstitutionId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken);
    Task<CoachingStudentReadAuthorization?> AuthorizeCoachingStudentReadAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentUserIds,
        CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>?> GetCoachingReportStudentUserIdsAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        CancellationToken cancellationToken);
    Task<CoachingReportStudentPage?> GetCoachingReportStudentPageAsync(
        Guid viewerUserId,
        Guid institutionId,
        int? gradeLevel,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

public sealed record CoachingTeacherAuthorization(Guid? InstitutionId);
public sealed record CoachingStudentReadAuthorization(IReadOnlyCollection<Guid> AllowedStudentUserIds);
public sealed record CoachingReportStudentPage(
    IReadOnlyCollection<Guid> StudentUserIds,
    int TotalCount);

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    void ClearTracking();
}

public interface ITeacherRepository
{
    Task AddAsync(TeacherProfile teacher, CancellationToken cancellationToken);
    Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<TeacherProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken);
    Task<TeacherProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddStudentAssignmentAsync(TeacherStudentAssignment assignment, CancellationToken cancellationToken);
    Task<TeacherStudentAssignment?> GetAssignmentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken);
}

public interface IStudentRepository
{
    Task AddAsync(StudentProfile student, CancellationToken cancellationToken);
    Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<StudentProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken);
    Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Invitation>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken);
    Task<List<PendingInvitationReadModel>> GetPendingWithInviterEmailAsync(
        string email,
        CancellationToken cancellationToken);
    Task<List<Invitation>> GetByInviterIdAsync(Guid inviterId, CancellationToken cancellationToken);
}

public sealed record PendingInvitationReadModel(
    Guid Id,
    string InviterEmail,
    InvitationType Type,
    InvitationStatus Status,
    string? Message,
    DateTime CreatedAt,
    DateTime ExpiresAt);
