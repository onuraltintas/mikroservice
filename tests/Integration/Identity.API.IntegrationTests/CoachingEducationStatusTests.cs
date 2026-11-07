using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using FluentAssertions;

namespace Identity.API.IntegrationTests;

// E3 #14 (user decision 06.11.2026): "Şu an durumun ne?" replaces "Kaçıncı sınıftasın?". Ortaokul and lise students keep the
// Student status with their grade (5–8 and 9–12); a lise graduate, a university student and a university graduate are statuses of
// their own with no grade and no school. One server rule decides the exams and the diploma grade question.
public sealed class CoachingEducationStatusTests
{
    private const CoachingEducationStatus Student = CoachingEducationStatus.Student;

    [Theory]
    [InlineData(CoachingEducationStatus.Student, 5, "Lgs")]
    [InlineData(CoachingEducationStatus.Student, 8, "Lgs")]
    [InlineData(CoachingEducationStatus.Student, 9, "Yks")]
    [InlineData(CoachingEducationStatus.Student, 12, "Yks")]
    [InlineData(CoachingEducationStatus.HighSchoolGraduate, null, "Yks")]
    [InlineData(CoachingEducationStatus.UniversityStudent, null, "Ags,Yds,Yokdil")]
    [InlineData(CoachingEducationStatus.UniversityGraduate, null, "Ags,Yds,Yokdil")]
    [InlineData(CoachingEducationStatus.Unknown, null, "Lgs,Yks,Ags,Yds,Yokdil")]
    public void TheExamsFollowTheEducationTableInItsOrder(CoachingEducationStatus status, int? grade, string exams)
    {
        string.Join(',', LearningPurposes.AllowedFor(status, grade)).Should().Be(exams);
        foreach (var purpose in new[] { LearningPurpose.Lgs, LearningPurpose.Yks, LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil })
            LearningPurposes.FitsEducation(purpose, status, grade).Should().Be(exams.Split(',').Contains(purpose.ToString()), $"{purpose} for {status} {grade}");
    }

    [Fact]
    // G1 (user decision 07.11.2026): AGS, YDS and YÖKDİL are for university students and graduates; YKS ends with lise.
    public void UniversityExamsAreOnlyForUniversityStudentsAndGraduates()
    {
        foreach (var status in Enum.GetValues<CoachingEducationStatus>().Where(x => x is not (CoachingEducationStatus.UniversityStudent
            or CoachingEducationStatus.UniversityGraduate or CoachingEducationStatus.Unknown)))
            foreach (var purpose in new[] { LearningPurpose.Ags, LearningPurpose.Yds, LearningPurpose.Yokdil })
                LearningPurposes.FitsEducation(purpose, status, status == Student ? 12 : null).Should().BeFalse($"{purpose} for {status}");
        foreach (var status in new[] { CoachingEducationStatus.UniversityStudent, CoachingEducationStatus.UniversityGraduate })
            LearningPurposes.FitsEducation(LearningPurpose.Yks, status, null).Should().BeFalse(status.ToString());
    }

    [Theory]
    [InlineData(CoachingEducationStatus.Student, 10, false)]
    [InlineData(CoachingEducationStatus.Student, 11, true)]
    [InlineData(CoachingEducationStatus.Student, 12, true)]
    [InlineData(CoachingEducationStatus.HighSchoolGraduate, null, true)]
    [InlineData(CoachingEducationStatus.UniversityStudent, null, false)]
    [InlineData(CoachingEducationStatus.UniversityGraduate, null, false)]
    [InlineData(CoachingEducationStatus.Unknown, null, false)]
    // G1 (07.11.2026): university students and graduates no longer choose YKS, so the OBP is asked of lise students and graduates only.
    public void TheDiplomaGradeIsAskedForYksFromEleventhGradeAndOfALiseGraduate(CoachingEducationStatus status, int? grade, bool asked)
    {
        LearningPurposes.DiplomaApplies(LearningPurpose.Yks, status, grade).Should().Be(asked);
        LearningPurposes.DiplomaApplies(LearningPurpose.Yds, status, grade).Should().BeFalse("only YKS uses the OBP");
        LearningPurposes.DiplomaApplies(null, status, grade).Should().Be(asked, "no exam yet keeps it");
    }

    [Fact]
    public void ANonStudentStatusHasNoGradeAndNoSchool()
    {
        var profile = CoachingStudentProfile.Create(Guid.NewGuid());
        profile.UpdateEducation(Student, 11, "Ankara Fen Lisesi", "ANKARA", "ÇANKAYA", "Fen Lisesi", "11-A", null);
        foreach (var status in new[] { CoachingEducationStatus.HighSchoolGraduate, CoachingEducationStatus.UniversityStudent, CoachingEducationStatus.UniversityGraduate })
        {
            FluentActions.Invoking(() => profile.UpdateEducation(status, 11, null, null, null, null, null, null)).Should().Throw<ArgumentException>();
            profile.UpdateEducation(Student, 11, "Ankara Fen Lisesi", "ANKARA", "ÇANKAYA", "Fen Lisesi", "11-A", null);
            // A school sent with a non-student status is not kept (the pages warn before saving).
            profile.UpdateEducation(status, null, "Ankara Fen Lisesi", "ANKARA", "ÇANKAYA", "Fen Lisesi", "11-A", null);
            (profile.EducationStatus, profile.GradeLevel, profile.SchoolName, profile.SchoolCity, profile.SchoolDistrict, profile.SchoolType, profile.ClassBranch)
                .Should().Be((status, (int?)null, (string?)null, (string?)null, (string?)null, (string?)null, (string?)null));
            profile.IsEducationComplete.Should().BeTrue();
        }
    }

    [Fact]
    public void AStudentChoosesAGradeFromFiveToTwelve()
    {
        var profile = CoachingStudentProfile.Create(Guid.NewGuid());
        foreach (var grade in new[] { 0, 1, 4, 13 })
            FluentActions.Invoking(() => profile.UpdateEducation(Student, grade, null, null, null, null, null, null)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => profile.UpdateEducation(Student, null, null, null, null, null, null, null)).Should().Throw<ArgumentException>();
        profile.UpdateEducation(Student, 5, "Ortaokul", null, null, null, null, null);
        (profile.GradeLevel, profile.SchoolName).Should().Be(((int?)5, "Ortaokul"));
    }

    [Fact]
    public void AnOlderGradeBelowFiveStaysUntilTheEducationChanges()
    {
        var profile = CoachingStudentProfile.Create(Guid.NewGuid());
        typeof(CoachingStudentProfile).GetProperty(nameof(CoachingStudentProfile.EducationStatus))!.SetValue(profile, Student);
        typeof(CoachingStudentProfile).GetProperty(nameof(CoachingStudentProfile.GradeLevel))!.SetValue(profile, 3);
        // Other details (a teacher's school edit) save with the unchanged older grade.
        profile.UpdateEducation(Student, 3, "İlkokul", null, null, null, null, null);
        profile.GradeLevel.Should().Be(3);
        FluentActions.Invoking(() => profile.UpdateEducation(Student, 4, null, null, null, null, null, null)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AGraduationYearBelongsToAGraduateStatus()
    {
        var profile = CoachingStudentProfile.Create(Guid.NewGuid());
        profile.UpdateEducation(CoachingEducationStatus.HighSchoolGraduate, null, null, null, null, null, null, 2024);
        profile.GraduationYear.Should().Be(2024);
        profile.UpdateEducation(CoachingEducationStatus.UniversityGraduate, null, null, null, null, null, null, 2025);
        profile.GraduationYear.Should().Be(2025);
        FluentActions.Invoking(() => profile.UpdateEducation(CoachingEducationStatus.UniversityStudent, null, null, null, null, null, null, 2024)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => profile.UpdateEducation(Student, 10, null, null, null, null, null, 2024)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TheStoredNumbersOfTheOlderStatusesDoNotMove()
    {
        ((int)CoachingEducationStatus.Unknown, (int)Student, (int)CoachingEducationStatus.HighSchoolGraduate,
            (int)CoachingEducationStatus.UniversityStudent, (int)CoachingEducationStatus.UniversityGraduate).Should().Be((0, 1, 2, 3, 4));
    }
}
