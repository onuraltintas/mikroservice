using Coaching.Domain.Profiles;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOwnedProfileDomainTests
{
    [Fact]
    public void StudentProfile_HasOwnIdentifierAndKeepsAccountIdentifier()
    {
        var userId = Guid.NewGuid();
        var profile = CoachingStudentProfile.Create(userId);

        Assert.Equal(userId, profile.UserId);
        Assert.NotEqual(userId, profile.Id);
        Assert.Equal(CoachingEducationStatus.Unknown, profile.EducationStatus);
        Assert.False(profile.IsEducationComplete);
        profile.UpdateEducation(CoachingEducationStatus.Student, 12, "Okul", "Ankara", null, "Lise", "A", null);
        Assert.True(profile.IsEducationComplete);
        Assert.Equal(12, profile.GradeLevel);
        profile.UpdateEducation(CoachingEducationStatus.Graduate, null, null, null, null, null, null, 2025);
        Assert.True(profile.IsEducationComplete);
        Assert.Null(profile.GradeLevel);
        Assert.Equal(2025, profile.GraduationYear);
    }

    [Fact]
    public void StudentProfile_RejectsInvalidEducationAndCanClearOptionalInformation()
    {
        Assert.Throws<ArgumentException>(() => CoachingStudentProfile.Create(Guid.Empty));
        var profile = CoachingStudentProfile.Create(Guid.NewGuid());
        Assert.Throws<ArgumentException>(() => profile.UpdateEducation(CoachingEducationStatus.Student, null, null, null, null, null, null, null));
        Assert.Throws<ArgumentException>(() => profile.UpdateEducation(CoachingEducationStatus.Student, 13, null, null, null, null, null, null));
        Assert.Throws<ArgumentException>(() => profile.UpdateEducation(CoachingEducationStatus.Graduate, 12, null, null, null, null, null, 2025));
        Assert.Throws<ArgumentException>(() => profile.UpdateEducation((CoachingEducationStatus)99, null, null, null, null, null, null, null));
        profile.UpdatePersonal(new DateOnly(2008, 1, 2), "Bio", null, false);
        Assert.Equal(new DateOnly(2008, 1, 2), profile.BirthDate);
        Assert.False(profile.ShareProgressWithTeachers);
        profile.UpdatePersonal(null, null, null, true);
        Assert.Null(profile.BirthDate);
        Assert.Null(profile.Bio);
        Assert.Throws<ArgumentException>(() => profile.UpdatePersonal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1), null, null, true));
    }

    [Fact]
    public void TeacherProfile_IsOwnedByUserAndDoesNotGrantInstitutionWideAccessByDefault()
    {
        var userId = Guid.NewGuid();
        var profile = CoachingTeacherProfile.Create(userId);
        Assert.Equal(userId, profile.UserId);
        Assert.NotEqual(userId, profile.Id);
        Assert.False(profile.CanViewAllInstitutionStudents);
        profile.UpdateDetails("Koç", ["Matematik"], 4, "Bio", null);
        Assert.Equal("Matematik", Assert.Single(profile.Subjects));
        Assert.Throws<ArgumentException>(() => profile.UpdateDetails(null, [""], 4, null, null));
        Assert.Throws<ArgumentException>(() => profile.UpdateDetails(null, [], -1, null, null));
    }

    [Fact]
    public void Assignment_UsesAccountIdentifiersAndPreservesEndedRelationships()
    {
        var teacherUserId = Guid.NewGuid();
        var studentUserId = Guid.NewGuid();
        var assignment = CoachingTeacherStudentAssignment.Create(teacherUserId, studentUserId, null, " Matematik ", teacherUserId);
        Assert.Equal(teacherUserId, assignment.TeacherUserId);
        Assert.Equal(studentUserId, assignment.StudentUserId);
        Assert.Null(assignment.InstitutionId);
        Assert.Equal("Matematik", assignment.Subject);
        Assert.True(assignment.IsActive);
        assignment.SetActive(false, teacherUserId);
        Assert.False(assignment.IsActive);
        Assert.NotNull(assignment.EndDate);
        assignment.SetActive(true, teacherUserId);
        Assert.True(assignment.IsActive);
        Assert.Null(assignment.EndDate);
        Assert.Throws<ArgumentException>(() => CoachingTeacherStudentAssignment.Create(teacherUserId, teacherUserId, null, null, teacherUserId));
        Assert.Throws<ArgumentException>(() => CoachingTeacherStudentAssignment.Create(Guid.Empty, studentUserId, null, null, teacherUserId));
        Assert.Throws<ArgumentException>(() => CoachingTeacherStudentAssignment.Create(teacherUserId, studentUserId, Guid.Empty, null, teacherUserId));
    }

    [Fact]
    public void Membership_IsProductLocalAndRejectsUnknownRoleOrAccount()
    {
        var institutionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var member = CoachingInstitutionMembership.Create(institutionId, userId, CoachingMemberRole.Student, Guid.NewGuid());
        Assert.Equal(institutionId, member.InstitutionId);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(CoachingMemberRole.Student, member.Role);
        member.SetActive(false, userId);
        Assert.False(member.IsActive);
        Assert.Throws<ArgumentException>(() => CoachingInstitutionMembership.Create(Guid.Empty, userId, CoachingMemberRole.Student, userId));
        Assert.Throws<ArgumentException>(() => CoachingInstitutionMembership.Create(institutionId, Guid.Empty, CoachingMemberRole.Student, userId));
        Assert.Throws<ArgumentException>(() => CoachingInstitutionMembership.Create(institutionId, userId, (CoachingMemberRole)99, userId));
    }
}
