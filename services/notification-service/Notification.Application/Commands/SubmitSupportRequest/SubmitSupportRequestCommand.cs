using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Notification.Application.Commands.SubmitSupportRequest;

public record SubmitSupportRequestCommand(
    string FirstName,
    string LastName,
    string Email,
    string Subject,
    string Message) : IRequest<Result<Guid>>;
