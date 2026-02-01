using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Entities;

using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Enums;

namespace Identity.Application.Interfaces;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    void Delete(User user);
    Task<PagedList<UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, CancellationToken cancellationToken);
    Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task<List<User>> GetUsersByRolesAsync(List<string> roleNames, CancellationToken cancellationToken);
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
}

public interface IInstitutionRepository
{
    Task AddAsync(Institution institution, CancellationToken cancellationToken);
    Task AddAdminAsync(InstitutionAdmin admin, CancellationToken cancellationToken);
    Task<Guid?> GetInstitutionIdByAdminIdAsync(Guid adminUserId, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITeacherRepository
{
    Task AddAsync(TeacherProfile teacher, CancellationToken cancellationToken);
    Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<TeacherProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddStudentAssignmentAsync(TeacherStudentAssignment assignment, CancellationToken cancellationToken);
    Task<TeacherStudentAssignment?> GetAssignmentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken);
}

public interface IStudentRepository
{
    Task AddAsync(StudentProfile student, CancellationToken cancellationToken);
    Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken);
    Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}

public interface IInvitationRepository
{
    Task AddAsync(Invitation invitation, CancellationToken cancellationToken);
    Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<List<Invitation>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken);
    Task<List<Invitation>> GetByInviterIdAsync(Guid inviterId, CancellationToken cancellationToken);
}
