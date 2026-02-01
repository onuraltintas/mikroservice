using EduPlatform.Shared.Kernel.Results;
using MediatR;

namespace Notification.Application.Commands.ReplyToSupportRequest;

public record ReplyToSupportRequestCommand(
    Guid SupportRequestId,
    string ReplyMessage) : IRequest<Result>;
