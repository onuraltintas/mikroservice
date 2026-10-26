using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.LeaveInstitution;

public record LeaveInstitutionCommand() : IRequest<Result>;
