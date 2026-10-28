using EduPlatform.Shared.Contracts.Coaching;
using MassTransit;

namespace Coaching.Application.Profiles;

public interface ICoachingProfileProvisioner
{
    Task ProvisionAsync(Guid userId, string role, CancellationToken cancellationToken, DateOnly? birthDate = null);
}

public sealed class CoachingProfileProvisioningConsumer(ICoachingProfileProvisioner profiles)
    : IConsumer<CoachingProfileProvisioningRequested>
{
    public Task Consume(ConsumeContext<CoachingProfileProvisioningRequested> context)
    {
        var message = context.Message;
        if (message.Product != "coaching" || message.SchemaVersion != 1
            || message.UserId == Guid.Empty || message.Role is not ("Student" or "Teacher"))
            throw new ArgumentException("Invalid Coaching profile provisioning event.");
        return profiles.ProvisionAsync(message.UserId, message.Role, context.CancellationToken, message.Role == "Student" ? message.BirthDate : null);
    }
}
