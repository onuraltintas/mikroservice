using Notification.Application.Commands.SubmitSupportRequest;

namespace Notification.Application.Interfaces;

public interface IIdentityInternalService
{
    Task<bool> ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId, CancellationToken cancellationToken = default);
}
