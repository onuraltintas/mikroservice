using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace Notification.Application.Commands.ReplyToSupportRequest;

public class ReplyToSupportRequestHandler : IRequestHandler<ReplyToSupportRequestCommand, Result>
{
    private readonly INotificationDbContext _context;
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;

    public ReplyToSupportRequestHandler(INotificationDbContext context, IEmailDeliveryQueue emailDeliveryQueue)
    {
        _context = context;
        _emailDeliveryQueue = emailDeliveryQueue;
    }

    public async Task<Result> Handle(ReplyToSupportRequestCommand request, CancellationToken cancellationToken)
    {
        var supportRequest = await _context.SupportRequests
            .FirstOrDefaultAsync(x => x.Id == request.SupportRequestId, cancellationToken);

        if (supportRequest == null)
        {
            return Result.Failure(new Error("SupportRequest.NotFound", "Destek talebi bulunamadı."));
        }

        // 1. Send Email
        var subject = $"RE: {supportRequest.Subject} - Destek Talebi Cevabı";
        var body = $@"
            <html>
                <body style='font-family: sans-serif; line-height: 1.6;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                        <h2 style='color: #4f46e5;'>Destek Talebiniz Yanıtlandı</h2>
                        <p>Merhaba <strong>{supportRequest.FirstName} {supportRequest.LastName}</strong>,</p>
                        <p>Aşağıdaki konu hakkındaki destek talebiniz admin ekibimiz tarafından yanıtlanmıştır:</p>
                        
                        <div style='background-color: #f9fafb; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                            <p style='margin: 0; font-size: 0.9em; color: #6b7280;'>Sizin Mesajınız:</p>
                            <p style='margin: 5px 0 0 0;'>{supportRequest.Message}</p>
                        </div>

                        <div style='background-color: #eef2ff; padding: 20px; border-radius: 8px; border-left: 4px solid #4f46e5;'>
                            <p style='margin: 0; font-size: 0.9em; color: #4f46e5; font-weight: bold;'>Admin Yanıtı:</p>
                            <p style='margin: 10px 0 0 0; white-space: pre-wrap;'>{request.ReplyMessage}</p>
                        </div>

                        <p style='margin-top: 30px; font-size: 0.8em; color: #9ca3af;'>
                            Bu e-posta EduPlatform otomatik sistemi tarafından gönderilmiştir. Lütfen doğrudan yanıtlamayınız.
                        </p>
                    </div>
                </body>
            </html>";

        await _emailDeliveryQueue.QueueAsync(
            CreateMessageId(supportRequest.Id, request.ReplyMessage),
            "SupportReply",
            supportRequest.Email,
            subject,
            body,
            cancellationToken);

        // 2. Mark as Processed
        supportRequest.Process(request.ReplyMessage);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static Guid CreateMessageId(Guid supportRequestId, string replyMessage)
    {
        var payload = Encoding.UTF8.GetBytes($"{supportRequestId:N}:{replyMessage}");
        var hash = SHA256.HashData(payload);
        return new Guid(hash.AsSpan(0, 16));
    }
}
