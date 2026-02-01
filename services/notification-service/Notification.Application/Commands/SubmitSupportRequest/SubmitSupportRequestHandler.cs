using EduPlatform.Shared.Kernel.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;

namespace Notification.Application.Commands.SubmitSupportRequest;

public class SubmitSupportRequestHandler : IRequestHandler<SubmitSupportRequestCommand, Result<Guid>>
{
    private readonly INotificationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IIdentityInternalService _identityService;

    public SubmitSupportRequestHandler(
        INotificationDbContext dbContext, 
        IEmailService emailService,
        IIdentityInternalService identityService)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _identityService = identityService;
    }

    public async Task<Result<Guid>> Handle(SubmitSupportRequestCommand request, CancellationToken cancellationToken)
    {
        var supportRequest = new SupportRequest(
            Guid.NewGuid(),
            request.FirstName,
            request.LastName,
            request.Email,
            request.Subject,
            request.Message
        );

        _dbContext.SupportRequests.Add(supportRequest);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Send acknowledgment email to user
        await SendAcknowledgmentEmail(request, cancellationToken);

        // Forward to Identity service to notify admins
        await _identityService.ForwardSupportRequestAsync(request, supportRequest.Id);

        return Result.Success(supportRequest.Id);
    }

    private async Task SendAcknowledgmentEmail(SubmitSupportRequestCommand request, CancellationToken cancellationToken)
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

        await _emailService.SendEmailAsync(request.Email, subject, body, cancellationToken);
    }
}
