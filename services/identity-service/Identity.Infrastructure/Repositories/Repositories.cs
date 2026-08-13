using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetUserProfile;
using Identity.Domain.Enums;

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
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalizedEmail = email.ToLowerInvariant();
        return await _context.Users
            .Include(u => u.Roles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);
    }

    public void Delete(User user)
    {
        _context.Users.Remove(user);
    }

    public async Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        return await _context.Users
            .Include(u => u.RefreshTokens)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken), cancellationToken);
    }

    public async Task<PagedList<UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken)
    {
        var query = _context.Users
            .AsNoTracking()
            .AsQueryable();

        if (institutionId.HasValue)
        {
            var scopedInstitutionId = institutionId.Value;
            query = query.Where(u =>
                _context.InstitutionAdmins.Any(a =>
                    a.UserId == u.Id &&
                    a.InstitutionId == scopedInstitutionId &&
                    a.IsActive) ||
                _context.TeacherProfiles.Any(t =>
                    t.UserId == u.Id &&
                    t.InstitutionId == scopedInstitutionId &&
                    t.IsActive) ||
                _context.StudentProfiles.Any(s =>
                    s.UserId == u.Id &&
                    s.InstitutionId == scopedInstitutionId &&
                    s.IsActive) ||
                _context.StudentProfiles.Any(s =>
                    s.ParentId == u.Id &&
                    s.InstitutionId == scopedInstitutionId &&
                    s.IsActive));
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(u => EF.Functions.ILike(u.Email, $"%{searchTerm}%"));
        }

        if (isActive.HasValue)
        {
            query = query.Where(u => u.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Roles.Any(r => r.Role.Name == role));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(u => u.Roles)
            .ThenInclude(ur => ur.Role)
            .ToListAsync(cancellationToken);

        var dtos = users.Select(u => new UserProfileDto
        {
            UserId = u.Id,
            Email = u.Email,
            Role = u.Roles.FirstOrDefault()?.Role?.Name ?? "Unknown",
            FirstName = u.FirstName,
            LastName = u.LastName,
            FullName = (string.IsNullOrWhiteSpace(u.FirstName) && string.IsNullOrWhiteSpace(u.LastName)) 
                ? "-" 
                : $"{u.FirstName} {u.LastName}".Trim(),
            IsActive = u.IsActive,
            EmailConfirmed = u.EmailConfirmed,
            PhoneNumber = u.PhoneNumber,
            LastLoginAt = u.LastLoginAt,
            Roles = u.Roles.Select(ur => ur.Role.Name).ToList()
        }).ToList();

        return new PagedList<UserProfileDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<List<User>> GetUsersByRolesAsync(List<string> roleNames, CancellationToken cancellationToken)
    {
        return await _context.Users
            .Where(u => u.Roles.Any(r => roleNames.Contains(r.Role.Name)))
            .ToListAsync(cancellationToken);
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
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == adminUserId && a.IsActive, cancellationToken);
        return admin?.InstitutionId;
    }

    public async Task<Guid?> GetPrimaryInstitutionIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var adminInstitutionId = await _context.InstitutionAdmins
            .AsNoTracking()
            .Where(a => a.UserId == userId && a.IsActive)
            .Select(a => (Guid?)a.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (adminInstitutionId.HasValue)
        {
            return adminInstitutionId;
        }

        var teacherInstitutionId = await _context.TeacherProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.IsActive && t.InstitutionId.HasValue)
            .Select(t => t.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (teacherInstitutionId.HasValue)
        {
            return teacherInstitutionId;
        }

        var studentInstitutionId = await _context.StudentProfiles
            .AsNoTracking()
            .Where(s => s.UserId == userId && s.IsActive && s.InstitutionId.HasValue)
            .Select(s => s.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentInstitutionId.HasValue)
        {
            return studentInstitutionId;
        }

        return await _context.StudentProfiles
            .AsNoTracking()
            .Where(s => s.ParentId == userId && s.IsActive && s.InstitutionId.HasValue)
            .Select(s => s.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsUserInInstitutionAsync(Guid userId, Guid institutionId, CancellationToken cancellationToken)
    {
        return await _context.InstitutionAdmins.AnyAsync(a =>
                   a.UserId == userId && a.InstitutionId == institutionId && a.IsActive,
                   cancellationToken)
            || await _context.TeacherProfiles.AnyAsync(t =>
                   t.UserId == userId && t.InstitutionId == institutionId && t.IsActive,
                   cancellationToken)
            || await _context.StudentProfiles.AnyAsync(s =>
                   s.UserId == userId && s.InstitutionId == institutionId && s.IsActive,
                   cancellationToken)
            || await _context.StudentProfiles.AnyAsync(s =>
                   s.ParentId == userId && s.InstitutionId == institutionId && s.IsActive,
                   cancellationToken);
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

public class ParentRepository : IParentRepository
{
    private readonly IdentityDbContext _context;
    public ParentRepository(IdentityDbContext context) => _context = context;

    public async Task AddAsync(ParentProfile parent, CancellationToken cancellationToken)
    {
        await _context.ParentProfiles.AddAsync(parent, cancellationToken);
    }

    public async Task<ParentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.ParentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<ParentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.ParentProfiles
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
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

public class RoleRepository : IRoleRepository
{
    private readonly IdentityDbContext _context;

    public RoleRepository(IdentityDbContext context)
    {
        _context = context;
    }

    public async Task<Role?> GetByNameAsync(string roleName, CancellationToken cancellationToken)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
    }

    public async Task<Role?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Role?> GetByIdWithPermissionsAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Role>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _context.Roles
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Role role, CancellationToken cancellationToken)
    {
        await _context.Roles.AddAsync(role, cancellationToken);
    }

    public void Delete(Role role)
    {
        _context.Roles.Remove(role);
    }

    public void AddRolePermission(RolePermission permission)
    {
        _context.RolePermissions.Add(permission);
    }

    public void RemoveRolePermission(RolePermission permission)
    {
        _context.RolePermissions.Remove(permission);
    }
}

