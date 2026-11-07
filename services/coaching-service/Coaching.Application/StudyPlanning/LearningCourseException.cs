namespace Coaching.Application.StudyPlanning;

/// <summary>
/// PR #128 review M2: Çalışma tercihlerim refused one chosen course (a lesson that does not fit, or its priority or difficulty);
/// the API returns it as field "courses" with the lesson's id, so Derslerim opens that course's row.
/// </summary>
public sealed class LearningCourseException(Guid lessonId, string message) : ArgumentException(message)
{
    public Guid LessonId { get; } = lessonId;
}
