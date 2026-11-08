using Coaching.Domain.Profiles;

namespace Coaching.Application.StudyPlanning;

/// <summary>Paket H (K5): the most study one day of the weekly routine may hold. Graduates who no longer attend school get 10 hours.</summary>
public static class WeeklyDayLimit
{
    public const int SchoolMinutes = 480;
    public const int GraduateMinutes = 600;

    public static int For(CoachingEducationStatus? status) =>
        status is CoachingEducationStatus.HighSchoolGraduate or CoachingEducationStatus.UniversityGraduate ? GraduateMinutes : SchoolMinutes;
}
