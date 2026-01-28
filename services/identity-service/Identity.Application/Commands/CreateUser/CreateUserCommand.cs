using EduPlatform.Shared.Kernel.Results;
using Identity.Domain.Enums;
using MediatR;

namespace Identity.Application.Commands.CreateUser;

public record CreateUserResponse(Guid UserId, string TemporaryPassword);

public record CreateUserCommand(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string Role
) : IRequest<Result<CreateUserResponse>>;
