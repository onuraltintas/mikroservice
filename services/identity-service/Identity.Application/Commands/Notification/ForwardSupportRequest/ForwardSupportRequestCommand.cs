using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Identity.Application.Commands.Notification.ForwardSupportRequest;

public record ForwardSupportRequestCommand(
    Guid SupportRequestId,
    string FirstName,
    string LastName,
    string Email,
    string Subject,
    string Message) : IRequest<Result>;
