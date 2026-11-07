using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Profiles;

/// <summary>
/// E3 #14 (user decision 06.11.2026), "Şu an durumun ne?": an ortaokul or lise student is a Student with the grade (5–8, 9–12);
/// a lise graduate (the earlier "Graduate", same stored number), a university student and a university graduate have no grade
/// and no school. The numbers are stored: never renumber.
/// </summary>
public enum CoachingEducationStatus { Unknown = 0, Student = 1, HighSchoolGraduate = 2, UniversityStudent = 3, UniversityGraduate = 4 }
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
    /// <summary>Paket C (#3): where the student lives, from the shared Identity location directory (ids and names).</summary>
    public string? HomeProvinceId { get; private set; }
    public string? HomeCity { get; private set; }
    public string? HomeDistrictId { get; private set; }
    public string? HomeDistrict { get; private set; }
    /// <summary>Paket C (#3): the YKS diploma grade (50–100) for OBP; Estimated when the student gave an expected grade.</summary>
    public decimal? DiplomaGrade { get; private set; }
    public bool DiplomaGradeEstimated { get; private set; }
    /// <summary>Paket C (#3): the first-login wizard steps saved so far (bit n = step n, 1–7) and when it was finished.</summary>
    public int OnboardingSteps { get; private set; }
    public DateTime? OnboardingCompletedAt { get; private set; }
    public bool IsEducationComplete => EducationStatus is CoachingEducationStatus.HighSchoolGraduate or CoachingEducationStatus.UniversityStudent
        or CoachingEducationStatus.UniversityGraduate || EducationStatus == CoachingEducationStatus.Student && GradeLevel.HasValue;

    public static CoachingStudentProfile Create(Guid userId)
    {
        ProfileValue.RequireId(userId);
        return new() { UserId = userId, CreatedBy = userId.ToString() };
    }

    /// <summary>E3 #14: the grades a student chooses (the wizard's and Profilim's 5–12).</summary>
    public const int FirstGrade = 5, LastGrade = 12;
    public static bool IsGraduate(CoachingEducationStatus status) =>
        status is CoachingEducationStatus.HighSchoolGraduate or CoachingEducationStatus.UniversityGraduate;

    /// <summary>
    /// E3 #14: a student needs a grade from 5 to 12 (an older grade below 5 is kept while the education does not change); every other
    /// status has no grade, and a non-student has no school, school details or branch (they are cleared; the pages warn before saving).
    /// A graduation year belongs to a graduate status.
    /// </summary>
    public void UpdateEducation(CoachingEducationStatus status, int? gradeLevel, string? schoolName,
        string? schoolCity, string? schoolDistrict, string? schoolType, string? classBranch, int? graduationYear)
    {
        var unchangedOlderGrade = status == EducationStatus && gradeLevel == GradeLevel && gradeLevel is >= 1 and < FirstGrade;
        if (!Enum.IsDefined(status) || gradeLevel is < 1 or > LastGrade
            || gradeLevel < FirstGrade && !unchangedOlderGrade
            || status == CoachingEducationStatus.Student && !gradeLevel.HasValue
            || status != CoachingEducationStatus.Student && gradeLevel.HasValue
            || graduationYear is < 1900 or > 9999
            || !IsGraduate(status) && graduationYear.HasValue
            || graduationYear > DateTime.UtcNow.Year)
            throw new ArgumentException("Geçerli eğitim durumu ve sınıf bilgisi belirtin.");
        var student = status == CoachingEducationStatus.Student;
        var school = student ? ProfileValue.Text(schoolName, 255) : null;
        var city = student ? ProfileValue.Text(schoolCity, 100) : null;
        var district = student ? ProfileValue.Text(schoolDistrict, 100) : null;
        var type = student ? ProfileValue.Text(schoolType, 100) : null;
        var branch = student ? ProfileValue.Text(classBranch, 30) : null;
        // E3 review L4: from lise (9th grade on) to ortaokul, the stored school is a lise (the catalog lists high schools) and the
        // branch a lise branch: they are dropped unless a different school or branch is given (the pages warn before saving).
        if (student && EducationStatus == CoachingEducationStatus.Student && GradeLevel >= 9 && gradeLevel <= 8)
        {
            if (school is not null && school == SchoolName) school = city = district = type = null;
            if (branch is not null && branch == ClassBranch) branch = null;
        }
        EducationStatus = status; GradeLevel = gradeLevel; GraduationYear = graduationYear;
        SchoolName = school; SchoolCity = city; SchoolDistrict = district; SchoolType = type; ClassBranch = branch;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// A copy holding the stored education, to validate a full replacement before the tracked profile changes (an older grade
    /// below 5 stays valid while it is unchanged).
    /// </summary>
    public CoachingStudentProfile CopyForValidation() => new()
    {
        UserId = UserId, CreatedBy = CreatedBy, EducationStatus = EducationStatus, GradeLevel = GradeLevel,
        SchoolName = SchoolName, ClassBranch = ClassBranch, GraduationYear = GraduationYear, BirthDate = BirthDate
    };

    /// <summary>
    /// E3 review M1: a teacher edits only the school details (not the education). A student's are replaced; a non-student has no
    /// school on the pages, so whatever is stored is left as it is.
    /// </summary>
    public void UpdateSchoolDetails(string? schoolName, string? schoolCity, string? schoolDistrict, string? schoolType, string? classBranch)
    {
        var school = ProfileValue.Text(schoolName, 255); var city = ProfileValue.Text(schoolCity, 100);
        var district = ProfileValue.Text(schoolDistrict, 100); var type = ProfileValue.Text(schoolType, 100); var branch = ProfileValue.Text(classBranch, 30);
        if (EducationStatus != CoachingEducationStatus.Student) return;
        SchoolName = school; SchoolCity = city; SchoolDistrict = district; SchoolType = type; ClassBranch = branch;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// E3 review L2: the one-off cutover keeps an older snapshot's grade as it was (1–12, like the database); a grade below 5 is
    /// asked again when the education next changes. Without a grade the education is unknown.
    /// </summary>
    public void ImportLegacyEducation(int? gradeLevel, string? schoolName, string? schoolCity)
    {
        if (gradeLevel is < 1 or > LastGrade) throw new ArgumentException("Geçerli eğitim durumu ve sınıf bilgisi belirtin.");
        var student = gradeLevel.HasValue;
        var school = student ? ProfileValue.Text(schoolName, 255) : null; var city = student ? ProfileValue.Text(schoolCity, 100) : null;
        EducationStatus = student ? CoachingEducationStatus.Student : CoachingEducationStatus.Unknown; GradeLevel = gradeLevel;
        SchoolName = school; SchoolCity = city; SchoolDistrict = null; SchoolType = null; ClassBranch = null; GraduationYear = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePersonal(DateOnly? birthDate, string? bio, string? avatarUrl, bool shareProgressWithTeachers)
    {
        // PR #128 review L1: the wizard's rule (Türkiye date, 6 to 100 years old) for a new birth date; a stored one sent back
        // unchanged is kept.
        if (birthDate is { } date && date != BirthDate)
        {
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, CoachingTime.Turkey));
            if (date > today) throw new ArgumentException("Doğum tarihi bugünden sonra olamaz.");
            if (date > today.AddYears(-6)) throw new ArgumentException("Doğum tarihi en az 6 yıl önce olmalı. Doğum tarihini kontrol et.");
            if (date < today.AddYears(-100)) throw new ArgumentException("Doğum tarihi en çok 100 yıl önce olabilir. Doğum tarihini kontrol et.");
        }
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

    /// <summary>Paket C (#3): call only after the shared Identity directory has verified this province and district pair.</summary>
    public void SetHome(string provinceId, string city, string districtId, string district)
    {
        var province = ProfileValue.Text(provinceId, 20) ?? throw new ArgumentException("Şehir seçin.");
        var cityName = ProfileValue.Text(city, 100) ?? throw new ArgumentException("Şehir seçin.");
        var districtCode = ProfileValue.Text(districtId, 20) ?? throw new ArgumentException("İlçe seçin.");
        var districtName = ProfileValue.Text(district, 100) ?? throw new ArgumentException("İlçe seçin.");
        HomeProvinceId = province; HomeCity = cityName; HomeDistrictId = districtCode; HomeDistrict = districtName;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Paket C (#3): 50–100 with at most two decimals; null clears it ("sonra gir").</summary>
    public void SetDiplomaGrade(decimal? grade, bool estimated)
    {
        if (grade is < 50 or > 100 || grade.HasValue && decimal.Round(grade.Value, 2) != grade.Value)
            throw new ArgumentException("Diploma notu 50 ile 100 arasında olmalı.");
        DiplomaGrade = grade; DiplomaGradeEstimated = grade.HasValue && estimated;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool HasOnboardingStep(int step) => step is >= 1 and <= OnboardingStepCount && (OnboardingSteps & (1 << step)) != 0;

    /// <summary>The first wizard step not saved yet (1–7); 7 is the summary.</summary>
    public int NextOnboardingStep => Enumerable.Range(1, OnboardingStepCount).FirstOrDefault(step => !HasOnboardingStep(step), OnboardingStepCount);

    public void RecordOnboardingStep(int step)
    {
        if (step is < 1 or >= OnboardingStepCount) throw new ArgumentOutOfRangeException(nameof(step));
        OnboardingSteps |= 1 << step; UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>A changed answer re-asks only the steps that depend on it.</summary>
    public void ReopenOnboardingStep(int step)
    {
        if (step is < 1 or >= OnboardingStepCount) throw new ArgumentOutOfRangeException(nameof(step));
        OnboardingSteps &= ~(1 << step); UpdatedAt = DateTime.UtcNow;
    }

    public void CompleteOnboarding(DateTime at)
    {
        if (Enumerable.Range(1, OnboardingStepCount - 1).Any(step => !HasOnboardingStep(step)))
            throw new InvalidOperationException("Kurulumun bütün adımlarını tamamla.");
        OnboardingSteps |= 1 << OnboardingStepCount; OnboardingCompletedAt ??= DateTime.SpecifyKind(at, DateTimeKind.Utc);
        UpdatedAt = DateTime.UtcNow;
    }

    public const int OnboardingStepCount = 7;
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
