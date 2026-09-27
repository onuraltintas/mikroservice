using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.DeleteExamResult;

public sealed record DeleteExamResultCommand(Guid ExamId, Guid ResultId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
