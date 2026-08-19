using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Application.Commands.SubmitSupportRequest;

public class SubmitSupportRequestHandler : IRequestHandler<SubmitSupportRequestCommand, Result<Guid>>
{
    private readonly INotificationDbContext _dbContext;
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly IIdentityInternalService _identityService;

    public SubmitSupportRequestHandler(
        INotificationDbContext dbContext, 
        IEmailDeliveryQueue emailDeliveryQueue,
        IIdentityInternalService identityService)
    {
        _dbContext = dbContext;
        _emailDeliveryQueue = emailDeliveryQueue;
        _identityService = identityService;
    }

    public async Task<Result<Guid>> Handle(SubmitSupportRequestCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var idempotencyKey = request.IdempotencyKey!.Trim();
        var existingRequest = await FindExistingAsync(normalizedEmail, idempotencyKey, cancellationToken);
        if (existingRequest is not null)
        {
            return Result.Success(existingRequest.Id);
        }

        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            request.FirstName,
            request.LastName,
            normalizedEmail,
            request.Subject,
            request.Message,
            idempotencyKey
        );

        _dbContext.SupportRequests.Add(supportRequest);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent retry may win the unique (email, key) insert. If
            // the row is now visible, return its original result and do not
            // send a second acknowledgement or admin notification.
            existingRequest = await FindExistingAsync(normalizedEmail, idempotencyKey, cancellationToken);
            if (existingRequest is null)
            {
                throw;
            }

            return Result.Success(existingRequest.Id);
        }

        // Send acknowledgment email to user
        await QueueAcknowledgmentEmail(request, supportRequest.Id, cancellationToken);

        // Forward to Identity service to notify admins
        await _identityService.ForwardSupportRequestAsync(request, supportRequest.Id, cancellationToken);

        return Result.Success(supportRequest.Id);
    }

    private Task<SupportRequest?> FindExistingAsync(
        string email,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return _dbContext.SupportRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Email == email && x.IdempotencyKey == idempotencyKey,
                cancellationToken);
    }

    private async Task QueueAcknowledgmentEmail(
        SubmitSupportRequestCommand request,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_SupportReceived" && t.IsActive, cancellationToken);

        string subject;
        string body;

        if (template != null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", request.FirstName)
                .Replace("{{LastName}}", request.LastName);

            body = template.Body
                .Replace("{{FirstName}}", request.FirstName)
                .Replace("{{LastName}}", request.LastName)
                .Replace("{{Subject}}", request.Subject)
                .Replace("{{Date}}", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        }
        else
        {
            subject = "Destek Talebiniz Alındı ✅";
            body = $@"<h1>Merhaba {request.FirstName}!</h1>
                      <p>Destek talebiniz başarıyla alınmıştır. En kısa sürede size dönüş yapacağız.</p>
                      <p><strong>Konu:</strong> {request.Subject}</p>";
        }

        await _emailDeliveryQueue.QueueAsync(
            messageId,
            "SupportAcknowledgement",
            request.Email,
            subject,
            body,
            cancellationToken);
    }
}
