using Notification.Application.Commands.SubmitSupportRequest;

namespace Notification.Application.Interfaces;

public interface IIdentityInternalService
{
    Task<SupportForwardResult> ForwardSupportRequestAsync(SubmitSupportRequestCommand request, Guid supportRequestId, CancellationToken cancellationToken = default);

    /// <summary>Checks the anonymous support form's reCAPTCHA token through Identity; false when it cannot be checked.</summary>
    Task<bool> VerifySupportCaptchaAsync(string? token, CancellationToken cancellationToken = default);
}

public enum SupportForwardResult
{
    Sent,
    /// <summary>Network error, timeout, 5xx, 408 or 429: worth retrying.</summary>
    Transient,
    /// <summary>Identity refused the request (other 4xx) or configuration is missing: retrying cannot help.</summary>
    Rejected
}
