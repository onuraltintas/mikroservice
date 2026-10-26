using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Application.Interfaces;
using MassTransit;

namespace Identity.Application.Services;

/// <summary>
/// Publishes a new account to the other services. The password-setup token goes only to Notification;
/// <see cref="UserCreatedEvent"/>, which every service receives, carries no token.
/// </summary>
public static class AccountCreatedEvents
{
    public static async Task PublishAsync(
        IPublishEndpoint publishEndpoint,
        ProvisionedUser provisionedUser,
        string email,
        string firstName,
        string lastName,
        string role,
        string? product,
        CancellationToken cancellationToken)
    {
        var createdAt = DateTime.UtcNow;
        await publishEndpoint.Publish(new UserPasswordSetupRequestedV1(
            provisionedUser.UserId, email, firstName, lastName, role, product,
            provisionedUser.PasswordSetupToken, provisionedUser.PasswordSetupTokenExpiresAt, createdAt), cancellationToken);
        await publishEndpoint.Publish(new UserCreatedEvent(
            provisionedUser.UserId, email, firstName, lastName, role,
            PasswordSetupToken: null, provisionedUser.PasswordSetupTokenExpiresAt, createdAt) { Product = product }, cancellationToken);
    }
}
