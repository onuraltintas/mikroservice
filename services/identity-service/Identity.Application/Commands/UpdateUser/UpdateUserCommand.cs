using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.UpdateUser;

public record UpdateUserCommand(
    Guid UserId, 
    string FirstName, 
    string LastName, 
    string? PhoneNumber
) : IRequest<Result>;
