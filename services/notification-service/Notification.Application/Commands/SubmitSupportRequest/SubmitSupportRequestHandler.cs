using System.Net;
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
        await using var transaction = await _dbContext.BeginTransactionAsync(cancellationToken);
        if (request.SubjectUserId is { } userId && await _dbContext.LockAndCheckErasedRecipientAsync(userId, cancellationToken))
            return Result.Failure<Guid>(Error.Forbidden("Bu hesap için destek talebi oluşturulamıyor."));
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

        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            request.FirstName,
            request.LastName,
            normalizedEmail,
            request.Subject,
            request.Message,
            idempotencyKey,
            request.SubjectUserId
        );

        _dbContext.SupportRequests.Add(supportRequest);
        _dbContext.SupportForwardDeliveries.Add(
            SupportForwardDelivery.Create(supportRequest.Id));
        try
        {
            // The form is anonymous and the address is whatever the sender typed, so the
            // acknowledgement must not become a way to mail arbitrary people repeatedly.
            if (await IsWithinAcknowledgementLimitAsync(normalizedEmail, cancellationToken))
            {
                await QueueAcknowledgmentEmail(request, supportRequest.Id, cancellationToken);
            }

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

    /// <summary>At most this many acknowledgements per address in 24 hours.</summary>
    public const int MaxAcknowledgementsPerAddressPerDay = 3;

    private async Task<bool> IsWithinAcknowledgementLimitAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        var since = DateTime.UtcNow.AddDays(-1);
        var recent = await _dbContext.SupportRequests
            .AsNoTracking()
            .CountAsync(x => x.Email == normalizedEmail && x.CreatedAt >= since, cancellationToken);
        return recent < MaxAcknowledgementsPerAddressPerDay;
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

        var firstName = WebUtility.HtmlEncode(request.FirstName);
        var lastName = WebUtility.HtmlEncode(request.LastName);
        var requestSubject = WebUtility.HtmlEncode(request.Subject);

        string subject;
        string body;

        if (template != null)
        {
            // Sender-typed names stay out of the subject line (the recipient address is not
            // verified, so the subject must not carry arbitrary text to a stranger).
            subject = string.Join(' ', template.Subject
                .Replace("{{FirstName}}", string.Empty)
                .Replace("{{LastName}}", string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries));

            body = template.Body
                .Replace("{{FirstName}}", firstName)
                .Replace("{{LastName}}", lastName)
                .Replace("{{Subject}}", requestSubject)
                .Replace("{{Date}}", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        }
        else
        {
            subject = "Destek Talebiniz Alındı ✅";
            body = $@"<h1>Merhaba {firstName}!</h1>
                      <p>Destek talebiniz başarıyla alınmıştır. En kısa sürede size dönüş yapacağız.</p>
                      <p><strong>Konu:</strong> {requestSubject}</p>";
        }

        await _emailDeliveryQueue.QueueAsync(
            messageId,
            "SupportAcknowledgement",
            request.Email,
            subject,
            body,
            request.SubjectUserId,
            cancellationToken);
    }
}
