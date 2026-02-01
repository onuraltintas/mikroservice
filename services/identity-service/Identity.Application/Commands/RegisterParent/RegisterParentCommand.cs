using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.RegisterParent;

public record RegisterParentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber
) : IRequest<Result<Guid>>;
