using Notification.Application.Commands.SubmitSupportRequest;

namespace Notification.Application.Interfaces;

public interface IIdentityInternalService
{
    Task ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId);
}
