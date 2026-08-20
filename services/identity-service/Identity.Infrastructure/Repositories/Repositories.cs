using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

using Identity.Application.Queries.GetAllUsers;
using Identity.Application.Queries.GetUserProfile;
using Identity.Application.DTOs.Institutions;
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
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.Permissions)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken), cancellationToken);
    }

    public async Task RevokeActiveRefreshTokensAsync(
        Guid userId,
        string reason,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _context.RefreshTokens
            .Where(token => token.UserId == userId
                && token.RevokedAt == null
                && token.ExpiresAt > now)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.Revoke("system", reason);
        }
    }

    public async Task<PagedList<UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken)
    {
        if (page is < 1 or > GetAllUsersQuery.MaxPageNumber)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize is < 1 or > GetAllUsersQuery.MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

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
                    a.IsActive &&
                    a.Institution.IsActive) ||
                _context.TeacherProfiles.Any(t =>
                    t.UserId == u.Id &&
                    t.InstitutionId == scopedInstitutionId &&
                    t.IsActive &&
                    t.Institution != null &&
                    t.Institution.IsActive) ||
                _context.StudentProfiles.Any(s =>
                    s.UserId == u.Id &&
                    s.InstitutionId == scopedInstitutionId &&
                    s.IsActive &&
                    s.Institution != null &&
                    s.Institution.IsActive) ||
                _context.StudentProfiles.Any(s =>
                    s.ParentId == u.Id &&
                    s.InstitutionId == scopedInstitutionId &&
                    s.IsActive &&
                    s.Institution != null &&
                    s.Institution.IsActive));
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

    public async Task<UserSummaryDto> GetSummaryAsync(
        Guid? institutionId,
        CancellationToken cancellationToken)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();
        if (institutionId.HasValue)
        {
            var scopedInstitutionId = institutionId.Value;
            query = query.Where(u =>
                _context.InstitutionAdmins.Any(a =>
                    a.UserId == u.Id && a.InstitutionId == scopedInstitutionId && a.IsActive && a.Institution.IsActive) ||
                _context.TeacherProfiles.Any(t =>
                    t.UserId == u.Id && t.InstitutionId == scopedInstitutionId && t.IsActive && t.Institution != null && t.Institution.IsActive) ||
                _context.StudentProfiles.Any(s =>
                    s.UserId == u.Id && s.InstitutionId == scopedInstitutionId && s.IsActive && s.Institution != null && s.Institution.IsActive) ||
                _context.StudentProfiles.Any(s =>
                    s.ParentId == u.Id && s.InstitutionId == scopedInstitutionId && s.IsActive && s.Institution != null && s.Institution.IsActive));
        }

        var totalUsers = await query.CountAsync(cancellationToken);
        var activeUsers = await query.CountAsync(user => user.IsActive, cancellationToken);
        return new UserSummaryDto(totalUsers, activeUsers, totalUsers - activeUsers);
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

    private sealed record TeacherReadProfile(Guid Id, Guid? InstitutionId, bool CanViewAllInstitutionStudents);
    private sealed record StudentReadProfile(Guid Id, Guid UserId, Guid? ParentId, Guid? InstitutionId);
    private sealed record TeacherStudentReadAssignment(Guid TeacherId, Guid StudentId, Guid? InstitutionId);

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

    public async Task<PagedList<InstitutionDto>> GetAllAsync(
        int page,
        int pageSize,
        string? searchTerm,
        bool? isActive,
        Guid? institutionId,
        CancellationToken cancellationToken)
    {
        var query = _context.Institutions.AsNoTracking().AsQueryable();

        if (institutionId.HasValue)
        {
            query = query.Where(institution => institution.Id == institutionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var normalizedSearch = searchTerm.Trim();
            var pattern = $"%{normalizedSearch.ToLowerInvariant()}%";
            query = query.Where(institution =>
                EF.Functions.Like(institution.Name.ToLower(), pattern)
                || (institution.City != null && EF.Functions.Like(institution.City.ToLower(), pattern))
                || (institution.Email != null && EF.Functions.Like(institution.Email.ToLower(), pattern)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(institution => institution.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var institutions = await query
            .OrderByDescending(institution => institution.CreatedAt)
            .ThenBy(institution => institution.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(institution => new InstitutionDto(
                institution.Id,
                institution.Name,
                institution.Type,
                institution.LogoUrl,
                institution.Address,
                institution.City,
                institution.District,
                institution.Phone,
                institution.Email,
                institution.Website,
                institution.LicenseType,
                institution.MaxStudents,
                institution.MaxTeachers,
                institution.SubscriptionStartDate,
                institution.SubscriptionEndDate,
                institution.IsActive,
                institution.Students.Count(student => student.IsActive),
                institution.Teachers.Count(teacher => teacher.IsActive),
                institution.Admins.Count(admin => admin.IsActive)))
            .ToListAsync(cancellationToken);

        return new PagedList<InstitutionDto>(institutions, totalCount, page, pageSize);
    }

    public Task<Institution?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Institutions.FirstOrDefaultAsync(institution => institution.Id == id, cancellationToken);
    }

    public Task<InstitutionDto?> GetDtoByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _context.Institutions
            .AsNoTracking()
            .Where(institution => institution.Id == id)
            .Select(institution => new InstitutionDto(
                institution.Id,
                institution.Name,
                institution.Type,
                institution.LogoUrl,
                institution.Address,
                institution.City,
                institution.District,
                institution.Phone,
                institution.Email,
                institution.Website,
                institution.LicenseType,
                institution.MaxStudents,
                institution.MaxTeachers,
                institution.SubscriptionStartDate,
                institution.SubscriptionEndDate,
                institution.IsActive,
                institution.Students.Count(student => student.IsActive),
                institution.Teachers.Count(teacher => teacher.IsActive),
                institution.Admins.Count(admin => admin.IsActive)))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<bool> HasAdminAsync(Guid institutionId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.InstitutionAdmins.AnyAsync(admin =>
            admin.InstitutionId == institutionId && admin.UserId == userId && admin.IsActive,
            cancellationToken);
    }

    public async Task<IReadOnlyList<InstitutionAdminDto>> GetAdminsAsync(
        Guid institutionId,
        CancellationToken cancellationToken)
    {
        return await _context.InstitutionAdmins
            .AsNoTracking()
            .Where(admin => admin.InstitutionId == institutionId)
            .OrderByDescending(admin => admin.IsActive)
            .ThenBy(admin => admin.User.Email)
            .Select(admin => new InstitutionAdminDto(
                admin.UserId,
                admin.User.Email,
                admin.User.FirstName,
                admin.User.LastName,
                admin.Role,
                admin.IsActive))
            .ToListAsync(cancellationToken);
    }

    public Task<InstitutionAdmin?> GetAdminAsync(
        Guid institutionId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        return _context.InstitutionAdmins.FirstOrDefaultAsync(
            admin => admin.InstitutionId == institutionId && admin.UserId == userId,
            cancellationToken);
    }

    public async Task<Guid?> GetInstitutionIdByAdminIdAsync(Guid adminUserId, CancellationToken cancellationToken)
    {
        var admin = await _context.InstitutionAdmins
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.UserId == adminUserId
                && a.IsActive
                && a.User.IsActive
                && a.Institution.IsActive,
                cancellationToken);
        return admin?.InstitutionId;
    }

    public async Task<Guid?> GetPrimaryInstitutionIdByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var adminInstitutionId = await _context.InstitutionAdmins
            .AsNoTracking()
            .Where(a => a.UserId == userId
                && a.IsActive
                && a.User.IsActive
                && a.Institution.IsActive)
            .OrderBy(a => a.CreatedAt)
            .ThenBy(a => a.Id)
            .Select(a => (Guid?)a.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (adminInstitutionId.HasValue)
        {
            return adminInstitutionId;
        }

        var teacherInstitutionId = await _context.TeacherProfiles
            .AsNoTracking()
            .Where(t => t.UserId == userId
                && t.IsActive
                && t.User.IsActive
                && t.InstitutionId.HasValue
                && t.Institution != null
                && t.Institution.IsActive)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => t.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (teacherInstitutionId.HasValue)
        {
            return teacherInstitutionId;
        }

        var studentInstitutionId = await _context.StudentProfiles
            .AsNoTracking()
            .Where(s => s.UserId == userId
                && s.IsActive
                && s.User.IsActive
                && s.InstitutionId.HasValue
                && s.Institution != null
                && s.Institution.IsActive)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Select(s => s.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);

        if (studentInstitutionId.HasValue)
        {
            return studentInstitutionId;
        }

        return await _context.StudentProfiles
            .AsNoTracking()
            .Where(s => s.ParentId == userId
                && s.IsActive
                && s.User.IsActive
                && s.InstitutionId.HasValue
                && s.Institution != null
                && s.Institution.IsActive)
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .Select(s => s.InstitutionId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsUserInInstitutionAsync(Guid userId, Guid institutionId, CancellationToken cancellationToken)
    {
        return await _context.InstitutionAdmins.AnyAsync(a =>
                   a.UserId == userId
                   && a.InstitutionId == institutionId
                   && a.IsActive
                   && a.User.IsActive
                   && a.Institution.IsActive,
                   cancellationToken)
            || await _context.TeacherProfiles.AnyAsync(t =>
                   t.UserId == userId
                   && t.InstitutionId == institutionId
                   && t.IsActive
                   && t.User.IsActive
                   && t.Institution != null
                   && t.Institution.IsActive,
                   cancellationToken)
            || await _context.StudentProfiles.AnyAsync(s =>
                   s.UserId == userId
                   && s.InstitutionId == institutionId
                   && s.IsActive
                   && s.User.IsActive
                   && s.Institution != null
                   && s.Institution.IsActive,
                   cancellationToken)
            || await _context.StudentProfiles.AnyAsync(s =>
                   s.ParentId == userId
                   && s.InstitutionId == institutionId
                   && s.IsActive
                   && s.User.IsActive
                   && s.Institution != null
                   && s.Institution.IsActive,
                   cancellationToken);
    }

    public async Task<CoachingTeacherAuthorization?> AuthorizeCoachingTeacherTargetsAsync(
        Guid teacherUserId,
        IReadOnlyCollection<Guid> studentUserIds,
        Guid? requestedInstitutionId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken)
    {
        var teacher = await _context.TeacherProfiles
            .AsNoTracking()
            .Include(profile => profile.User)
                .ThenInclude(user => user.Roles)
                    .ThenInclude(userRole => userRole.Role)
            .Include(profile => profile.Institution)
            .FirstOrDefaultAsync(profile =>
                profile.UserId == teacherUserId
                && profile.IsActive
                && profile.User.IsActive
                && profile.User.Roles.Any(userRole =>
                    userRole.Role.Name == "Teacher" && !userRole.Role.IsDeleted)
                && (!profile.InstitutionId.HasValue
                    || (profile.Institution != null && profile.Institution.IsActive)),
                cancellationToken);

        if (teacher == null)
        {
            return null;
        }

        if (!isSystemAdministrator
            && requestedInstitutionId.HasValue
            && teacher!.InstitutionId != requestedInstitutionId)
        {
            return null;
        }

        var institutionId = requestedInstitutionId ?? teacher!.InstitutionId;

        if (isSystemAdministrator && requestedInstitutionId.HasValue)
        {
            var activeInstitution = await _context.Institutions
                .AsNoTracking()
                .AnyAsync(institution =>
                    institution.Id == requestedInstitutionId.Value && institution.IsActive,
                    cancellationToken);

            if (!activeInstitution)
            {
                return null;
            }
        }

        var distinctStudentUserIds = studentUserIds.Distinct().ToArray();
        if (distinctStudentUserIds.Length == 0)
        {
            return new CoachingTeacherAuthorization(institutionId);
        }

        var students = await _context.StudentProfiles
            .AsNoTracking()
            .Include(profile => profile.User)
            .Include(profile => profile.Institution)
            .Where(profile =>
                distinctStudentUserIds.Contains(profile.UserId)
                && profile.IsActive
                && profile.User.IsActive
                && profile.InstitutionId == institutionId
                && (!profile.InstitutionId.HasValue
                    || (profile.Institution != null && profile.Institution.IsActive)))
            .Select(profile => new { profile.UserId, profile.Id })
            .ToListAsync(cancellationToken);

        if (students.Count != distinctStudentUserIds.Length)
        {
            return null;
        }

        if (!isSystemAdministrator)
        {
            var studentProfileIds = students.Select(student => student.Id).ToArray();
            var assignedStudentProfileIds = await _context.TeacherStudentAssignments
                .AsNoTracking()
                .Where(assignment =>
                    assignment.TeacherId == teacher!.Id
                    && assignment.IsActive
                    && studentProfileIds.Contains(assignment.StudentId)
                    && assignment.InstitutionId == institutionId)
                .Select(assignment => assignment.StudentId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (assignedStudentProfileIds.Count != studentProfileIds.Length)
            {
                return null;
            }
        }

        return new CoachingTeacherAuthorization(institutionId);
    }

    public async Task<CoachingStudentReadAuthorization?> AuthorizeCoachingStudentReadAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentUserIds,
        CancellationToken cancellationToken)
    {
        var distinctStudentUserIds = studentUserIds.Distinct().ToArray();
        if (distinctStudentUserIds.Length == 0)
        {
            return new CoachingStudentReadAuthorization(Array.Empty<Guid>());
        }

        var viewer = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == viewerUserId && user.IsActive)
            .Select(user => new
            {
                user.Id,
                Roles = user.Roles
                    .Where(userRole => !userRole.Role.IsDeleted)
                    .Select(userRole => userRole.Role.Name)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (viewer is null)
        {
            return null;
        }

        var isSystemAdministrator = viewer.Roles.Any(role =>
            string.Equals(role, "SystemAdmin", StringComparison.OrdinalIgnoreCase));
        var isParent = viewer.Roles.Any(role =>
            string.Equals(role, "Parent", StringComparison.OrdinalIgnoreCase));
        var isInstitutionAdministrator = viewer.Roles.Any(role =>
            string.Equals(role, "InstitutionAdmin", StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "InstitutionOwner", StringComparison.OrdinalIgnoreCase));
        var isTeacher = viewer.Roles.Any(role =>
            string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase));
        var isStudent = viewer.Roles.Any(role =>
            string.Equals(role, "Student", StringComparison.OrdinalIgnoreCase));

        if (!isSystemAdministrator
            && !isParent
            && !isInstitutionAdministrator
            && !isTeacher
            && !isStudent)
        {
            return null;
        }

        var activeParentProfile = isParent && await _context.ParentProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.UserId == viewerUserId && profile.IsActive, cancellationToken);

        var institutionAdminIds = isInstitutionAdministrator
            ? await _context.InstitutionAdmins
                .AsNoTracking()
                .Where(admin => admin.UserId == viewerUserId
                    && admin.IsActive
                    && admin.Institution.IsActive)
                .Select(admin => admin.InstitutionId)
                .ToListAsync(cancellationToken)
            : new List<Guid>();

        var teacherProfiles = isTeacher
            ? await _context.TeacherProfiles
                .AsNoTracking()
                .Where(profile => profile.UserId == viewerUserId
                    && profile.IsActive
                    && profile.User.IsActive
                    && (!profile.InstitutionId.HasValue
                        || (profile.Institution != null && profile.Institution.IsActive)))
                .Select(profile => new TeacherReadProfile(
                    profile.Id,
                    profile.InstitutionId,
                    profile.CanViewAllInstitutionStudents))
                .ToListAsync(cancellationToken)
            : new List<TeacherReadProfile>();

        var studentProfiles = await _context.StudentProfiles
            .AsNoTracking()
            .Where(profile => distinctStudentUserIds.Contains(profile.UserId)
                && profile.IsActive
                && profile.User.IsActive
                && (!profile.InstitutionId.HasValue
                    || (profile.Institution != null && profile.Institution.IsActive)))
            .Select(profile => new StudentReadProfile(
                profile.Id,
                profile.UserId,
                profile.ParentId,
                profile.InstitutionId))
            .ToListAsync(cancellationToken);

        var teacherProfileIds = teacherProfiles
            .Select(profile => (Guid)profile.Id)
            .ToArray();
        var studentProfileIds = studentProfiles
            .Select(profile => profile.Id)
            .ToArray();
        var teacherAssignments = teacherProfileIds.Length == 0 || studentProfileIds.Length == 0
            ? new List<TeacherStudentReadAssignment>()
            : await _context.TeacherStudentAssignments
                .AsNoTracking()
                .Where(assignment => assignment.IsActive
                    && teacherProfileIds.Contains(assignment.TeacherId)
                    && studentProfileIds.Contains(assignment.StudentId))
                .Select(assignment => new TeacherStudentReadAssignment(
                    assignment.TeacherId,
                    assignment.StudentId,
                    assignment.InstitutionId))
                .ToListAsync(cancellationToken);

        var allowedStudentUserIds = studentProfiles
            .Where(profile =>
                isSystemAdministrator
                || (isStudent && profile.UserId == viewerUserId)
                || (activeParentProfile && profile.ParentId == viewerUserId)
                || (institutionAdminIds.Contains(profile.InstitutionId ?? Guid.Empty)
                    && profile.InstitutionId.HasValue)
                || (teacherProfiles.Any(teacher =>
                    teacher.CanViewAllInstitutionStudents
                        && teacher.InstitutionId == profile.InstitutionId
                        && profile.InstitutionId.HasValue)
                    || teacherAssignments.Any(assignment =>
                        assignment.StudentId == profile.Id
                        && teacherProfiles.Any(teacher =>
                            teacher.Id == assignment.TeacherId
                            && teacher.InstitutionId == profile.InstitutionId)
                        && assignment.InstitutionId == profile.InstitutionId)))
            .Select(profile => profile.UserId)
            .Distinct()
            .ToArray();

        return new CoachingStudentReadAuthorization(allowedStudentUserIds);
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
    
    public Task<TeacherProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return GetByUserIdAsync(userId, null, cancellationToken);
    }

    public async Task<TeacherProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken)
    {
        IQueryable<TeacherProfile> query = _context.TeacherProfiles
            .Include(t => t.Institution)
            .Where(t => t.UserId == userId
                && t.IsActive
                && t.User.IsActive
                && (!t.InstitutionId.HasValue
                    || (t.Institution != null && t.Institution.IsActive)));

        if (institutionId.HasValue)
        {
            query = query.Where(t => t.InstitutionId == institutionId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TeacherProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.TeacherProfiles
            .Include(t => t.Institution)
            .FirstOrDefaultAsync(t => t.Id == id
                && t.IsActive
                && t.User.IsActive
                && (!t.InstitutionId.HasValue
                    || (t.Institution != null && t.Institution.IsActive)),
                cancellationToken);
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
    
    public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        return GetByUserIdAsync(userId, null, cancellationToken);
    }

    public async Task<StudentProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken)
    {
        IQueryable<StudentProfile> query = _context.StudentProfiles
            .Include(s => s.Institution)
            .Where(s => s.UserId == userId
                && s.IsActive
                && s.User.IsActive
                && (!s.InstitutionId.HasValue
                    || (s.Institution != null && s.Institution.IsActive)));

        if (institutionId.HasValue)
        {
            query = query.Where(s => s.InstitutionId == institutionId.Value);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return await _context.StudentProfiles
            .Include(s => s.Institution)
            .FirstOrDefaultAsync(s => s.Id == id
                && s.IsActive
                && s.User.IsActive
                && (!s.InstitutionId.HasValue
                    || (s.Institution != null && s.Institution.IsActive)),
                cancellationToken);
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

    public async Task RemovePermissionFromAllRolesAsync(
        string permissionKey,
        CancellationToken cancellationToken)
    {
        var assignments = await _context.RolePermissions
            .Where(permission => permission.Permission == permissionKey)
            .ToListAsync(cancellationToken);
        _context.RolePermissions.RemoveRange(assignments);
    }
}

