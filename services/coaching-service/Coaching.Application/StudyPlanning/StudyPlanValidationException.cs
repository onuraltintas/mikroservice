namespace Coaching.Application.StudyPlanning;

/// <summary>An invalid planning request whose message is written for the student and is shown as is.</summary>
public sealed class StudyPlanValidationException(string message) : ArgumentException(message);
