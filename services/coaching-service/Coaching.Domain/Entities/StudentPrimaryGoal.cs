using EduPlatform.Shared.Kernel.Primitives;
namespace Coaching.Domain.Entities;

public sealed class StudentPrimaryGoal : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid GoalId { get; private set; }
    private StudentPrimaryGoal() { }
    public static StudentPrimaryGoal Create(Guid student, Guid goal) => new() { StudentId = student, GoalId = goal };
    public void Select(Guid goal) { GoalId = goal; UpdatedAt = DateTime.UtcNow; }
}
