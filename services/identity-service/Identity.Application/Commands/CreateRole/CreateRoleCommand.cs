using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.CreateRole;

public record CreateRoleCommand(string Name, string Description) : IRequest<Result<Guid>>;
