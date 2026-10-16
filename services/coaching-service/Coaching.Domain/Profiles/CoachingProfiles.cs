using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Profiles;

public enum CoachingEducationStatus { Unknown = 0, Student = 1, Graduate = 2 }
public enum CoachingMemberRole { Student = 1, Teacher = 2 }

public sealed class CoachingStudentProfile : AggregateRoot
{
    private CoachingStudentProfile() { }
    public Guid UserId { get; private set; }
    public CoachingEducationStatus EducationStatus { get; private set; }
    public int? GradeLevel { get; private set; }
    public string? SchoolName { get; private set; }
    public string? SchoolCity { get; private set; }
    public string? SchoolDistrict { get; private set; }
    public string? SchoolType { get; private set; }
    public string? ClassBranch { get; private set; }
    public int? GraduationYear { get; private set; }
    public DateOnly? BirthDate { get; private set; }
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool ShareProgressWithTeachers { get; private set; } = true;
    public bool IsActive { get; private set; } = true;
    public bool IsEducationComplete => EducationStatus == CoachingEducationStatus.Graduate
        || EducationStatus == CoachingEducationStatus.Student && GradeLevel.HasValue;

    public static CoachingStudentProfile Create(Guid userId)
    {
        ProfileValue.RequireId(userId);
        return new() { UserId = userId, CreatedBy = userId.ToString() };
    }

    public void UpdateEducation(CoachingEducationStatus status, int? gradeLevel, string? schoolName,
        string? schoolCity, string? schoolDistrict, string? schoolType, string? classBranch, int? graduationYear)
    {
        if (!Enum.IsDefined(status) || gradeLevel is < 1 or > 12
            || status == CoachingEducationStatus.Student && !gradeLevel.HasValue
            || status != CoachingEducationStatus.Student && gradeLevel.HasValue
            || graduationYear is < 1900 or > 9999
            || status != CoachingEducationStatus.Graduate && graduationYear.HasValue
            || graduationYear > DateTime.UtcNow.Year)
            throw new ArgumentException("Geçerli eğitim durumu ve sınıf bilgisi belirtin.");
        var school = ProfileValue.Text(schoolName, 255);
        var city = ProfileValue.Text(schoolCity, 100);
        var district = ProfileValue.Text(schoolDistrict, 100);
        var type = ProfileValue.Text(schoolType, 100);
        var branch = ProfileValue.Text(classBranch, 30);
        EducationStatus = status; GradeLevel = gradeLevel; GraduationYear = graduationYear;
        SchoolName = school; SchoolCity = city; SchoolDistrict = district; SchoolType = type; ClassBranch = branch;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePersonal(DateOnly? birthDate, string? bio, string? avatarUrl, bool shareProgressWithTeachers)
    {
        if (birthDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw new ArgumentException("Doğum tarihi gelecekte olamaz.");
        var biography = ProfileValue.Text(bio, 2000);
        var avatar = ProfileValue.Text(avatarUrl, 500);
        if (avatar is not null && (!Uri.TryCreate(avatar, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Profil görseli geçerli bir HTTPS adresi olmalıdır.");
        BirthDate = birthDate; Bio = biography; AvatarUrl = avatar; ShareProgressWithTeachers = shareProgressWithTeachers;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active, Guid actorUserId)
    {
        ProfileValue.RequireId(actorUserId);
        IsActive = active; UpdatedBy = actorUserId.ToString(); UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class CoachingTeacherProfile : AggregateRoot
{
    private CoachingTeacherProfile() { }
    public Guid UserId { get; private set; }
    public string? Title { get; private set; }
    private string SubjectsJson { get; set; } = "[]";
    public IReadOnlyList<string> Subjects => JsonSerializer.Deserialize<string[]>(SubjectsJson)!;
    public int? ExperienceYears { get; private set; }
    public string? Bio { get; private set; }
    public string? AvatarUrl { get; private set; }
    public bool CanViewAllInstitutionStudents { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static CoachingTeacherProfile Create(Guid userId)
    {
        ProfileValue.RequireId(userId);
        return new() { UserId = userId, CreatedBy = userId.ToString() };
    }

    public void UpdateDetails(string? title, IReadOnlyCollection<string> subjects, int? experienceYears,
        string? bio, string? avatarUrl)
    {
        ArgumentNullException.ThrowIfNull(subjects);
        if (subjects.Count > 50 || experienceYears is < 0 or > 100)
            throw new ArgumentException("Geçerli branş ve deneyim bilgisi belirtin.");
        var normalized = subjects.Select(x => ProfileValue.Text(x, 100)
            ?? throw new ArgumentException("Branş boş olamaz.")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var validatedTitle = ProfileValue.Text(title, 100);
        var biography = ProfileValue.Text(bio, 2000);
        var avatar = ProfileValue.Text(avatarUrl, 500);
        if (avatar is not null && (!Uri.TryCreate(avatar, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw new ArgumentException("Profil görseli geçerli bir HTTPS adresi olmalıdır.");
        Title = validatedTitle; SubjectsJson = JsonSerializer.Serialize(normalized); ExperienceYears = experienceYears;
        Bio = biography; AvatarUrl = avatar; UpdatedAt = DateTime.UtcNow;
    }

    public void SetInstitutionWideAccess(bool allowed, Guid actorUserId)
    {
        ProfileValue.RequireId(actorUserId);
        CanViewAllInstitutionStudents = allowed; UpdatedBy = actorUserId.ToString(); UpdatedAt = DateTime.UtcNow;
    }

    public void SetActive(bool active, Guid actorUserId)
    {
        ProfileValue.RequireId(actorUserId);
        IsActive = active; UpdatedBy = actorUserId.ToString(); UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class CoachingInstitutionMembership : AggregateRoot
{
    private CoachingInstitutionMembership() { }
    public Guid InstitutionId { get; private set; }
    public Guid UserId { get; private set; }
    public CoachingMemberRole Role { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static CoachingInstitutionMembership Create(Guid institutionId, Guid userId, CoachingMemberRole role, Guid actorUserId)
    {
        ProfileValue.RequireId(institutionId); ProfileValue.RequireId(userId); ProfileValue.RequireId(actorUserId);
        if (!Enum.IsDefined(role)) throw new ArgumentException("Geçersiz Koçluk üye rolü.");
        return new() { InstitutionId = institutionId, UserId = userId, Role = role, CreatedBy = actorUserId.ToString() };
    }

    public void SetActive(bool active, Guid actorUserId)
    {
        ProfileValue.RequireId(actorUserId);
        IsActive = active; UpdatedBy = actorUserId.ToString(); UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class CoachingTeacherStudentAssignment : AggregateRoot
{
    private CoachingTeacherStudentAssignment() { }
    public Guid TeacherUserId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public Guid? InstitutionId { get; private set; }
    public string? Subject { get; private set; }
    public DateTime StartDate { get; private set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    public static CoachingTeacherStudentAssignment Create(Guid teacherUserId, Guid studentUserId,
        Guid? institutionId, string? subject, Guid actorUserId)
    {
        ProfileValue.RequireId(teacherUserId); ProfileValue.RequireId(studentUserId); ProfileValue.RequireId(actorUserId);
        if (teacherUserId == studentUserId || institutionId == Guid.Empty)
            throw new ArgumentException("Geçersiz öğretmen, öğrenci veya kurum ilişkisi.");
        return new() { TeacherUserId = teacherUserId, StudentUserId = studentUserId, InstitutionId = institutionId,
            Subject = ProfileValue.Text(subject, 100), CreatedBy = actorUserId.ToString() };
    }

    public void SetActive(bool active, Guid actorUserId)
    {
        ProfileValue.RequireId(actorUserId);
        IsActive = active; EndDate = active ? null : DateTime.UtcNow;
        UpdatedBy = actorUserId.ToString(); UpdatedAt = DateTime.UtcNow;
    }
}

internal static class ProfileValue
{
    internal static void RequireId(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("Kullanıcı veya kurum kimliği gereklidir.");
    }

    internal static string? Text(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Trim().Length > maxLength) throw new ArgumentException("Alan izin verilen uzunluğu aşıyor.");
        return value.Trim();
    }
}
