using EduPlatform.Shared.Security.Interfaces;

namespace Coaching.Application.Authorization;

public interface ICoachingAccessPolicy
{
    Guid? CurrentUserId { get; }
    bool IsSystemAdministrator { get; }
    bool IsCurrentTeacher(Guid teacherId);
    bool IsCurrentStudent(Guid studentId);
    Guid RequireCurrentTeacher();
    void RequireTeacher(Guid teacherId);
    void RequireStudent(Guid studentId);
    void RequireTeacherOrStudent(Guid teacherId, Guid studentId);
    void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds);
}

public sealed class CoachingAccessPolicy : ICoachingAccessPolicy
{
    private readonly ICurrentUserService _currentUser;

    public CoachingAccessPolicy(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public bool IsSystemAdministrator => IsSystemAdmin();

    public Guid? CurrentUserId => _currentUser.UserId;

    public bool IsCurrentTeacher(Guid teacherId) =>
        !IsSystemAdmin() && HasRole("Teacher") && _currentUser.UserId == teacherId;

    public bool IsCurrentStudent(Guid studentId) =>
        !IsSystemAdmin() && HasRole("Student") && _currentUser.UserId == studentId;

    public Guid RequireCurrentTeacher()
    {
        RequireRole("Teacher");
        return RequireAuthenticated();
    }

    public void RequireTeacher(Guid teacherId)
    {
        if (IsSystemAdmin())
        {
            return;
        }

        RequireRole("Teacher");
        RequireCurrentUser(teacherId, "Öğretmen kaynağı");
    }

    public void RequireStudent(Guid studentId)
    {
        if (IsSystemAdmin())
        {
            return;
        }

        RequireRole("Student");
        RequireCurrentUser(studentId, "Öğrenci kaynağı");
    }

    public void RequireTeacherOrStudent(Guid teacherId, Guid studentId)
    {
        if (IsSystemAdmin())
        {
            return;
        }

        var currentUserId = RequireAuthenticated();
        var isTeacher = HasRole("Teacher") && currentUserId == teacherId;
        var isStudent = HasRole("Student") && currentUserId == studentId;

        if (!isTeacher && !isStudent)
        {
            throw Forbidden("Bu Coaching kaynağına erişim yetkiniz yok.");
        }
    }

    public void RequireTeacherOrAssignedStudent(Guid teacherId, IEnumerable<Guid> studentIds)
    {
        if (IsSystemAdmin())
        {
            return;
        }

        var currentUserId = RequireAuthenticated();
        var isTeacher = HasRole("Teacher") && currentUserId == teacherId;
        var isAssignedStudent = HasRole("Student") && studentIds.Contains(currentUserId);

        if (!isTeacher && !isAssignedStudent)
        {
            throw Forbidden("Bu Coaching kaynağına erişim yetkiniz yok.");
        }
    }

    private bool IsSystemAdmin() => HasRole("SystemAdmin");

    private bool HasRole(string role) => _currentUser.Roles.Any(currentRole =>
        string.Equals(currentRole, role, StringComparison.OrdinalIgnoreCase));

    private Guid RequireAuthenticated()
    {
        return _currentUser.UserId
            ?? throw Forbidden("Oturum açılmış kullanıcı bulunamadı.");
    }

    private void RequireRole(string role)
    {
        RequireAuthenticated();
        if (!HasRole(role))
        {
            throw Forbidden("Bu Coaching işlemi için gerekli role sahip değilsiniz.");
        }
    }

    private void RequireCurrentUser(Guid resourceUserId, string resourceName)
    {
        if (RequireAuthenticated() != resourceUserId)
        {
            throw Forbidden($"{resourceName} yalnızca sahibi tarafından kullanılabilir.");
        }
    }

    private static EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException Forbidden(string message) =>
        new("Authorization.Forbidden", message);
}
