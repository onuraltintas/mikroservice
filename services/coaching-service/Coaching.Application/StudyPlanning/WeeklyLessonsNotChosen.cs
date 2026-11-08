using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Application.StudyPlanning;

/// <summary>
/// Paket H1 (P10, review of PR #129): the weekly draft still has lessons that are no longer chosen, so the yearly preview stops.
/// The conflict carries its own code and the lessons (id and the name saved with the weekly card); the API returns them as
/// <c>lessons</c> next to the message, so the student can remove them in one step.
/// </summary>
public static class WeeklyLessonsNotChosen
{
    public const string Code = "StudyPlanning.LessonsNotChosen";
    private const string LessonsKey = "lessons";

    /// <summary>The student reads it in second person; a teacher (<paramref name="staff"/>) in third person.</summary>
    public static BusinessRuleException Create(IReadOnlyList<WeeklyUnscheduledLesson> lessons, bool staff)
    {
        var names = string.Join(", ", lessons.Select(x => x.LessonName));
        var error = new BusinessRuleException(Code, staff
            ? $"Öğrencinin haftalık düzeninde artık seçili olmayan dersler var: {names}. Bu derslerin haftalık düzenden kaldırılması gerekiyor."
            : $"Haftalık düzeninde artık seçili olmayan dersler var: {names}. Bu dersleri haftalık düzeninden kaldırıp yeniden önizle.");
        error.Data[LessonsKey] = lessons.ToArray();
        return error;
    }

    public static IReadOnlyList<WeeklyUnscheduledLesson> Lessons(BusinessRuleException error) =>
        error.Data[LessonsKey] as WeeklyUnscheduledLesson[] ?? [];
}
