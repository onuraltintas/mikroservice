using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IdentityDbContext _context;

    public UserRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Users
            .Include(u => u.Roles) // Need roles for logic
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }
}

public class InstitutionRepository : IInstitutionRepository
{
    private readonly IdentityDbContext _context;

    public InstitutionRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Institution institution, CancellationToken cancellationToken)
    {
        await _context.Institutions.AddAsync(institution, cancellationToken);
    }

    public async Task AddAdminAsync(InstitutionAdmin admin, CancellationToken cancellationToken)
    {
        await _context.InstitutionAdmins.AddAsync(admin, cancellationToken);
    }

    public async Task<Guid?> GetInstitutionIdByAdminIdAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var admin = await _context.InstitutionAdmins
            .FirstOrDefaultAsync(a => a.UserId == adminUserId, cancellationToken);
        return admin?.InstitutionId;
    }
}

public class UnitOfWork : IUnitOfWork
{
    private readonly IdentityDbContext _context;

    public UnitOfWork(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}

public class TeacherRepository : ITeacherRepository
{
    private readonly IdentityDbContext _context;
    public TeacherRepository(IdentityDbContext context) => _context = context;

    public async Task AddAsync(TeacherProfile teacher, CancellationToken cancellationToken)
    {
        await _context.TeacherProfiles.AddAsync(teacher, cancellationToken);
    }
    
    public async Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.TeacherProfiles
            .Include(t => t.Institution)
            .FirstOrDefaultAsync(t => t.UserId == userId, cancellationToken);
    }

    public async Task<TeacherProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.TeacherProfiles
            .Include(t => t.Institution)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }
    
    public async Task AddStudentAssignmentAsync(TeacherStudentAssignment assignment, CancellationToken cancellationToken)
    {
        await _context.TeacherStudentAssignments.AddAsync(assignment, cancellationToken);
    }

    public async Task<TeacherStudentAssignment?> GetAssignmentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken)
    {
        return await _context.TeacherStudentAssignments
            .FirstOrDefaultAsync(a => a.TeacherId == teacherId && a.StudentId == studentId && a.IsActive, cancellationToken);
    }
}

public class StudentRepository : IStudentRepository
{
    private readonly IdentityDbContext _context;
    public StudentRepository(IdentityDbContext context) => _context = context;

    public async Task AddAsync(StudentProfile student, CancellationToken cancellationToken)
    {
        await _context.StudentProfiles.AddAsync(student, cancellationToken);
    }
    
    public async Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.StudentProfiles
            .Include(s => s.Institution)
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);
    }

    public async Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.StudentProfiles
            .Include(s => s.Institution)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }
}

public class InvitationRepository : IInvitationRepository
{
    private readonly IdentityDbContext _context;
    public InvitationRepository(IdentityDbContext context) => _context = context;

    public async Task AddAsync(Invitation invitation, CancellationToken cancellationToken)
    {
        await _context.Invitations.AddAsync(invitation, cancellationToken);
    }

    public async Task<Invitation?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Invitations
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
    }

    public async Task<List<Invitation>> GetPendingByEmailAsync(string email, CancellationToken cancellationToken)
    {
        return await _context.Invitations
            .Where(i => i.InviteeEmail == email.ToLowerInvariant() 
                && i.Status == Identity.Domain.Enums.InvitationStatus.Pending
                && i.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Invitation>> GetByInviterIdAsync(Guid inviterId, CancellationToken cancellationToken)
    {
        return await _context.Invitations
            .Where(i => i.InviterId == inviterId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
