using EduPlatform.Shared.Contracts.Events.Notification;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MassTransit;
using MediatR;

namespace Identity.Application.Commands.Notification.ForwardSupportRequest;

public class ForwardSupportRequestHandler : IRequestHandler<ForwardSupportRequestCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IPublishEndpoint _publishEndpoint;

    public ForwardSupportRequestHandler(IUserRepository userRepository, IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result> Handle(ForwardSupportRequestCommand request, CancellationToken cancellationToken)
    {
        // 1. Find Admins
        var admins = await _userRepository.GetUsersByRolesAsync(new List<string> { "SystemAdmin", "Admin" }, cancellationToken);

        if (admins == null || !admins.Any())
        {
            return Result.Failure(new Error("Notification.NoAdminFound", "Bildirim gönderilecek yetkili bulunamadı."));
        }

        // 2. Schedule notifications for each admin
        foreach (var admin in admins)
        {
            await _publishEndpoint.Publish(new SendNotificationEvent(
                admin.Id,
                "Yeni Destek Talebi 🆘",
                $"{request.FirstName} {request.LastName} ({request.Email}) tarafından yeni bir destek talebi oluşturuldu.\n\nKonu: {request.Subject}\n\nMesaj: {request.Message}",
                "SupportRequest",
                request.SupportRequestId.ToString()
            ), cancellationToken);
        }

        return Result.Success();
    }
}
