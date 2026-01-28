using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RecordUserLogin;

public record RecordUserLoginCommand(Guid UserId) : IRequest<Result>;
