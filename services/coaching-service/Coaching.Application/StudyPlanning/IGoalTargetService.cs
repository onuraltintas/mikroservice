using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coaching.Domain.Enums;

namespace Coaching.Application.StudyPlanning;

public sealed record GoalTargetUpdate([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    Guid? TargetUniversityProgramId, Guid? TargetSchoolId);
public sealed record GoalCatalogTargetView(string Name, string Detail, bool IsActive);
public sealed record GoalScoreTargetUpdate([property: JsonRequired] [Range(0, int.MaxValue)] int ExpectedVersion,
    [property: JsonRequired] decimal? TargetScore, [property: JsonRequired] decimal? MaxScore,
    [property: JsonRequired] ExamType? ExamType);
public sealed record GoalScoreTargetView(decimal? TargetScore, decimal? MaxScore, ExamType? ExamType, string? Subject);
public sealed record GoalTargetView(Guid GoalId, int Version, Guid? TargetUniversityProgramId, Guid? TargetSchoolId, bool CanEdit,
    GoalCatalogTargetView? CatalogTarget = null, GoalScoreTargetView? ScoreTarget = null);

public interface IGoalTargetService
{
    Task<GoalTargetView?> GetAsync(Guid goalId, CancellationToken cancellationToken = default);
    Task<GoalTargetView> ReplaceAsync(Guid goalId, GoalTargetUpdate request, CancellationToken cancellationToken = default);
    Task<GoalTargetView> ReplaceScoreAsync(Guid goalId, GoalScoreTargetUpdate request, CancellationToken cancellationToken = default);
}
