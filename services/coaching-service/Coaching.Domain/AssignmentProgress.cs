using Coaching.Domain.Enums;

namespace Coaching.Domain;

/// <summary>
/// Roadmap D2: the one meaning of "done" and "overdue" for a student's assignment, used by every list, count and summary.
/// Done: submitted or graded (a teacher may grade work handed in on paper). Overdue: still open (assigned or in progress)
/// on an active assignment after its due time; cancelled and closed assignments are never overdue.
/// Database queries spell out the same conditions.
/// </summary>
public static class AssignmentProgress
{
    public static bool IsDone(StudentAssignmentStatus status) =>
        status is StudentAssignmentStatus.Submitted or StudentAssignmentStatus.Graded;

    public static bool IsOpen(StudentAssignmentStatus status) =>
        status is StudentAssignmentStatus.Assigned or StudentAssignmentStatus.InProgress;

    public static bool IsOverdue(AssignmentStatus assignment, StudentAssignmentStatus status, DateTime dueDate, DateTime now) =>
        assignment == AssignmentStatus.Active && IsOpen(status) && dueDate < now;
}
