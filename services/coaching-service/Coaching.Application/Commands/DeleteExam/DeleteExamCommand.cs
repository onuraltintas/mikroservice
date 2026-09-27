using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.DeleteExam;

public record DeleteExamCommand(Guid ExamId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
