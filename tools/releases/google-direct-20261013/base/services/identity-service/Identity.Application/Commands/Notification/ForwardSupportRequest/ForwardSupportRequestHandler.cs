using EduPlatform.Shared.Contracts.Events.Notification;
using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Interfaces;
using MassTransit;
using MediatR;
using System.Security.Cryptography;
using System.Text;

namespace Identity.Application.Commands.Notification.ForwardSupportRequest;

public class ForwardSupportRequestHandler : IRequestHandler<ForwardSupportRequestCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublishEndpoint _publishEndpoint;

    public ForwardSupportRequestHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPublishEndpoint publishEndpoint)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
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
            var message = new SendNotificationEvent(
                admin.Id,
                "Yeni Destek Talebi 🆘",
                $"{request.FirstName} {request.LastName} ({request.Email}) tarafından yeni bir destek talebi oluşturuldu.\n\nKonu: {request.Subject}\n\nMesaj: {request.Message}",
                "SupportRequest",
                request.SupportRequestId.ToString());

            // Stable MessageId makes an at-least-once HTTP retry collapse in
            // Notification's inbox table for the same support request/admin.
            await _publishEndpoint.Publish(message, publishContext =>
            {
                publishContext.MessageId = CreateMessageId(request.SupportRequestId, admin.Id);
            }, cancellationToken);
        }

        // Identity uses MassTransit's EF bus outbox. Publish queues messages in
        // the current DbContext; save them before the HTTP call returns.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Guid CreateMessageId(Guid supportRequestId, Guid adminId)
    {
        var payload = Encoding.UTF8.GetBytes($"support:{supportRequestId:N}:admin:{adminId:N}");
        var hash = SHA256.HashData(payload);
        return new Guid(hash.AsSpan(0, 16));
    }
}
