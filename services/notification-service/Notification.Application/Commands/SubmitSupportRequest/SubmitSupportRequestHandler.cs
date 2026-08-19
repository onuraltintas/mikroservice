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

    public SubmitSupportRequestHandler(
        INotificationDbContext dbContext, 
        IEmailDeliveryQueue emailDeliveryQueue)
    {
        _dbContext = dbContext;
        _emailDeliveryQueue = emailDeliveryQueue;
    }

    public async Task<Result<Guid>> Handle(SubmitSupportRequestCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var idempotencyKey = request.IdempotencyKey!.Trim();
        var existingRequest = await FindExistingAsync(normalizedEmail, idempotencyKey, cancellationToken);
        if (existingRequest is not null)
        {
            if (!existingRequest.HasSamePayload(
                    request.FirstName,
                    request.LastName,
                    normalizedEmail,
                    request.Subject,
                    request.Message))
            {
                return Result.Failure<Guid>(Error.Conflict(
                    "This idempotency key has already been used with a different request payload."));
            }

            return Result.Success(existingRequest.Id);
        }

        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
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
        _dbContext.SupportForwardDeliveries.Add(
            SupportForwardDelivery.Create(supportRequest.Id));
        try
        {
            await QueueAcknowledgmentEmail(request, supportRequest.Id, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            // A concurrent retry may win the unique (email, key) insert. If
            // the row is now visible, return its original result and do not
            // send a second acknowledgement or admin notification.
            existingRequest = await FindExistingAsync(normalizedEmail, idempotencyKey, cancellationToken);
            if (existingRequest is null)
            {
                throw;
            }

            if (!existingRequest.HasSamePayload(
                    request.FirstName,
                    request.LastName,
                    normalizedEmail,
                    request.Subject,
                    request.Message))
            {
                return Result.Failure<Guid>(Error.Conflict(
                    "This idempotency key has already been used with a different request payload."));
            }

            return Result.Success(existingRequest.Id);
        }

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
