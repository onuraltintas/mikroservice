using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Coaching.Application.StudyPlanning;

public sealed record GoalTargetUpdate([property: JsonRequired, Range(0, int.MaxValue)] int ExpectedVersion,
    Guid? TargetUniversityProgramId, Guid? TargetSchoolId);
public sealed record GoalCatalogTargetView(string Name, string Detail, bool IsActive);
public sealed record GoalTargetView(Guid GoalId, int Version, Guid? TargetUniversityProgramId, Guid? TargetSchoolId, bool CanEdit,
    GoalCatalogTargetView? CatalogTarget = null);

public interface IGoalTargetService
{
    Task<GoalTargetView?> GetAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<GoalTargetView> ReplaceAsync(Guid goalId, GoalTargetUpdate request, CancellationToken cancellationToken = default);
}
