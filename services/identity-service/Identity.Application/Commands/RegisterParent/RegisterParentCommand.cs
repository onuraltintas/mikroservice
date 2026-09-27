using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.RegisterParent;

public record RegisterParentCommand(
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    PlatformProduct? Product = null
) : IRequest<Result<Guid>>;
