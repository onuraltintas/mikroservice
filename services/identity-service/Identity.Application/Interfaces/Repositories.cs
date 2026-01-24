using EduPlatform.Shared.Kernel.Primitives;
using Identity.Domain.Entities;

namespace Identity.Application.Interfaces;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    void Delete(User user);
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
