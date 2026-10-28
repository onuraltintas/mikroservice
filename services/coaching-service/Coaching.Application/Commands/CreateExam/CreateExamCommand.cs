using Coaching.Domain.Enums;

using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.CreateExam;

public record CreateExamCommand(
    Guid TeacherId,
    string Title,
    ExamType Type,
    DateTime ExamDate,
    decimal MaxScore,
    Guid? InstitutionId,
    string? Description,
    string? IdempotencyKey = null
) : IRequest<CreateExamResponse>
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }

    /// <summary>Roadmap O4: the grade the exam is for, chosen when it is created.</summary>
    public int? TargetGradeLevel { get; init; }
}

public record CreateExamResponse(Guid ExamId);
