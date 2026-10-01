using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using SpeedReading.Application.Content;
using SpeedReading.Application.Notifications;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingEmailTemplates(ISpeedReadingDataContext db) : ISpeedReadingEmailTemplates
{
    public async Task<IReadOnlyList<EmailTemplateSummary>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await db.EmailTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<EmailTemplateSummary?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        return row is null ? null : ToSummary(row);
    }

    public async Task<EmailTemplateSummary> CreateAsync(CreateEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Code, request.Subject, request.Body);
        var row = new LegacyEmailTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Code = request.Code.Trim().ToUpperInvariant(),
            Subject = request.Subject.Trim(),
            Body = request.Body,
            Description = Normalize(request.Description),
            Variables = Normalize(request.AvailableVariables),
            AvailableVariables = Normalize(request.AvailableVariables),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow
        };
        db.EmailTemplates.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateEmailTemplateRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Code, request.Subject, request.Body);
        var row = await db.EmailTemplates
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;

        row.Name = request.Name.Trim();
        row.Code = request.Code.Trim().ToUpperInvariant();
        row.Subject = request.Subject.Trim();
        row.Body = request.Body;
        row.Description = Normalize(request.Description);
        row.Variables = Normalize(request.AvailableVariables);
        row.AvailableVariables = Normalize(request.AvailableVariables);
        row.IsActive = request.IsActive;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailTemplates
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        row.IsDeleted = true;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmailTemplatePreview?> PreviewAsync(
        Guid id,
        IReadOnlyDictionary<string, string>? variables,
        CancellationToken cancellationToken)
    {
        var row = await db.EmailTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return null;

        var body = row.Body;
        if (variables is not null)
        {
            foreach (var pair in variables)
            {
                body = body.Replace($"{{{{{pair.Key}}}}}", pair.Value ?? string.Empty, StringComparison.Ordinal);
            }
        }

        return new EmailTemplatePreview(row.Subject, body);
    }

    private static EmailTemplateSummary ToSummary(LegacyEmailTemplate row) =>
        new(
            row.Id,
            row.Name,
            string.IsNullOrWhiteSpace(row.Code) ? row.Name : row.Code,
            row.Subject,
            row.Body,
            row.Description ?? string.Empty,
            row.AvailableVariables ?? row.Variables ?? string.Empty,
            row.IsActive,
            row.CreatedAt,
            row.UpdatedAt);

    private static void Validate(string name, string code, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length > 100)
            throw new ArgumentException("Code is required and must not exceed 100 characters.");
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 500)
            throw new ArgumentException("Subject is required and must not exceed 500 characters.");
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

internal sealed class LegacySpeedReadingEmailCampaigns(
    ISpeedReadingDataContext db,
    ISpeedReadingEmailDelivery emailDelivery,
    IDataProtectionProvider dataProtectionProvider,
    TimeProvider timeProvider,
    string publicBaseUrl,
    ILogger<LegacySpeedReadingEmailCampaigns> logger) : ISpeedReadingEmailCampaigns
{
    private const string QueueConsumerType = "SpeedReadingNewsletterCampaign";
    private readonly IDataProtector tokenProtector = dataProtectionProvider.CreateProtector("EduPlatform.SpeedReading.NewsletterTokens.v1");
    private readonly string normalizedPublicBaseUrl = ValidateBaseUrl(publicBaseUrl);

    public async Task<IReadOnlyList<EmailCampaignSummary>> GetAllAsync(int? status, CancellationToken cancellationToken)
    {
        var query = db.EmailCampaigns.AsNoTracking().Where(item => !item.IsDeleted);
        if (status == 2)
        {
            query = query.Where(item => item.Status == "Queued" || item.Status == "Sending");
        }
        else if (status.HasValue)
        {
            var statusName = StatusName(status.Value);
            if (statusName is not null) query = query.Where(item => item.Status == statusName);
        }

        var rows = await query.OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<EmailCampaignDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return null;

        var logs = await db.EmailCampaignLogs.AsNoTracking()
            .Where(item => item.CampaignId == id && !item.IsDeleted)
            .OrderByDescending(item => item.QueuedAt ?? item.CreatedAt)
            .Take(100)
            .Select(item => new EmailCampaignLogSummary(
                item.Id,
                item.RecipientEmail,
                item.Status,
                item.QueuedAt,
                item.SentAt,
                item.ErrorMessage))
            .ToListAsync(cancellationToken);

        return new EmailCampaignDetail(ToSummary(row), row.Body, row.PlainTextBody, logs);
    }

    public async Task<EmailCampaignSummary> CreateAsync(
        Guid userId,
        CreateEmailCampaignRequest request,
        CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Subject, request.Body);
        ValidateAudience(request.TargetRoles, request.TargetInstitutionId, request.IncludeAllUsers, request.IncludeSubscribers);
        var scheduledFor = NormalizeSchedule(request.ScheduledFor);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (scheduledFor.HasValue && scheduledFor <= now)
            throw new ArgumentException("Scheduled campaign time must be in the future.", nameof(request));

        var row = new LegacyEmailCampaign
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            Body = request.Body,
            PlainTextBody = Normalize(request.PlainTextBody),
            IncludeAllUsers = false,
            IncludeSubscribers = true,
            ScheduledFor = scheduledFor,
            Status = scheduledFor.HasValue ? "Scheduled" : "Draft",
            CreatedByUserId = userId,
            CreatedAt = now
        };
        db.EmailCampaigns.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateEmailCampaignRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Subject, request.Body);
        ValidateAudience(request.TargetRoles, request.TargetInstitutionId, request.IncludeAllUsers, request.IncludeSubscribers);
        var scheduledFor = NormalizeSchedule(request.ScheduledFor);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (scheduledFor.HasValue && scheduledFor <= now)
            throw new ArgumentException("Scheduled campaign time must be in the future.", nameof(request));

        var row = await db.EmailCampaigns.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        if (row.Status is "Sent" or "Sending" or "Queued" or "Failed")
            throw new InvalidOperationException("A campaign that has started queueing cannot be edited. Create a new draft instead.");

        row.Name = request.Name.Trim();
        row.Subject = request.Subject.Trim();
        row.Body = request.Body;
        row.PlainTextBody = Normalize(request.PlainTextBody);
        row.IncludeAllUsers = false;
        row.IncludeSubscribers = true;
        row.ScheduledFor = scheduledFor;
        row.Status = scheduledFor.HasValue ? "Scheduled" : "Draft";
        row.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns.SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        if (row.Status is "Sending" or "Queued" or "Sent")
            throw new InvalidOperationException("A campaign that has started queueing cannot be deleted.");
        row.IsDeleted = true;
        row.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmailCampaignSummary?> SendAsync(
        Guid id,
        SendEmailCampaignRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.SendNow)
        {
            var scheduled = await db.EmailCampaigns.SingleOrDefaultAsync(
                item => item.Id == id && !item.IsDeleted,
                cancellationToken);
            if (scheduled is null) return null;
            if (!scheduled.ScheduledFor.HasValue || scheduled.ScheduledFor <= timeProvider.GetUtcNow().UtcDateTime)
                throw new InvalidOperationException("Set a future schedule time or confirm immediate queueing.");
            return ToSummary(scheduled);
        }

        return await QueueCampaignAsync(id, forceNow: true, cancellationToken);
    }

    public async Task<EmailCampaignStats?> GetStatsAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return null;

        return new EmailCampaignStats(
            row.TotalRecipients,
            row.QueuedCount,
            row.SentCount,
            row.FailedCount,
            row.OpenedCount,
            row.ClickedCount,
            Math.Max(0, row.TotalRecipients - row.QueuedCount - row.SentCount - row.FailedCount));
    }

    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var ids = await db.EmailCampaigns.AsNoTracking()
            .Where(item => !item.IsDeleted
                && ((item.Status == "Scheduled" && item.ScheduledFor <= now) || item.Status == "Sending"))
            .OrderBy(item => item.ScheduledFor ?? item.CreatedAt)
            .Take(20)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var processed = 0;
        foreach (var id in ids)
        {
            try
            {
                await QueueCampaignAsync(id, forceNow: false, cancellationToken);
                processed++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Scheduled newsletter campaign {CampaignId} could not be queued", id);
            }
        }

        return processed;
    }

    private async Task<EmailCampaignSummary?> QueueCampaignAsync(
        Guid id,
        bool forceNow,
        CancellationToken cancellationToken)
    {
        var campaign = await db.EmailCampaigns.SingleOrDefaultAsync(
            item => item.Id == id && !item.IsDeleted,
            cancellationToken);
        if (campaign is null) return null;
        if (campaign.Status is "Queued" or "Sent") return ToSummary(campaign);

        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (!forceNow && campaign.Status == "Scheduled" && campaign.ScheduledFor > now)
            return ToSummary(campaign);
        if (campaign.Status is not ("Draft" or "Scheduled" or "Failed" or "Sending"))
            throw new InvalidOperationException("This campaign cannot be queued in its current state.");

        var logs = await db.EmailCampaignLogs
            .Where(item => item.CampaignId == id && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        if (campaign.Status == "Failed")
        {
            foreach (var failedLog in logs.Where(item => item.Status == "QueueFailed"))
            {
                failedLog.Status = "Pending";
                failedLog.ErrorMessage = null;
            }
        }

        if (logs.Count == 0)
        {
            ValidateAudience(campaign.TargetRoles, campaign.TargetInstitutionId, campaign.IncludeAllUsers, campaign.IncludeSubscribers);
            var recipients = await db.NewsletterSubscribers.AsNoTracking()
                .Where(item => !item.IsDeleted
                    && item.IsActive
                    && item.Status == CmsNewsletterStatuses.Active
                    && item.ConsentedAt.HasValue
                    && item.ConfirmedAt.HasValue
                    && item.PrivacyPolicyVersion.HasValue
                    && item.ConsentStatementVersion != null
                    && item.ConsentStatementVersion.StartsWith("speed-reading-newsletter-consent-v")
                    && item.UnsubscribeTokenProtected != null)
                .OrderBy(item => item.Email)
                .ToListAsync(cancellationToken);

            logs = recipients.Select(subscriber => new LegacyEmailCampaignLog
            {
                Id = Guid.NewGuid(),
                CampaignId = id,
                RecipientEmail = subscriber.Email,
                Status = "Pending",
                NewsletterUnsubscribeTokenProtected = subscriber.UnsubscribeTokenProtected,
                CreatedAt = now,
                CreatedBy = campaign.CreatedByUserId
            }).ToList();
            db.EmailCampaignLogs.AddRange(logs);
        }

        campaign.ScheduledFor = forceNow ? null : campaign.ScheduledFor;
        campaign.Status = "Sending";
        campaign.TotalRecipients = logs.Count;
        campaign.QueuedCount = logs.Count(item => item.Status == "Queued");
        campaign.FailedCount = logs.Count(item => item.Status == "QueueFailed");
        campaign.UpdatedAt = now;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (db is DbContext context)
            {
                foreach (var entry in context.ChangeTracker.Entries()
                    .Where(entry => entry.State == EntityState.Added || entry.Entity is LegacyEmailCampaign))
                {
                    entry.State = EntityState.Detached;
                }
            }

            var current = await db.EmailCampaigns.AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
            return current is null ? null : ToSummary(current);
        }

        await DispatchPendingLogsAsync(campaign, logs, cancellationToken);
        return ToSummary(campaign);
    }

    private async Task DispatchPendingLogsAsync(
        LegacyEmailCampaign campaign,
        IReadOnlyList<LegacyEmailCampaignLog> logs,
        CancellationToken cancellationToken)
    {
        foreach (var log in logs.Where(item => item.Status == "Pending"))
        {
            try
            {
                var unsubscribeToken = tokenProtector.Unprotect(log.NewsletterUnsubscribeTokenProtected
                    ?? throw new CryptographicException("The unsubscribe token is missing."));
                await emailDelivery.QueueAsync(
                    log.Id,
                    QueueConsumerType,
                    log.RecipientEmail,
                    campaign.Subject,
                    BuildCampaignBody(campaign.Body, unsubscribeToken),
                    cancellationToken);
                log.Status = "Queued";
                log.QueuedAt = timeProvider.GetUtcNow().UtcDateTime;
                log.ErrorMessage = null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                log.Status = "QueueFailed";
                log.ErrorMessage = exception.Message.Length <= 2_000
                    ? exception.Message
                    : exception.Message[..2_000];
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        campaign.QueuedCount = logs.Count(item => item.Status == "Queued");
        campaign.FailedCount = logs.Count(item => item.Status == "QueueFailed");
        campaign.TotalRecipients = logs.Count;
        campaign.Status = campaign.FailedCount > 0 ? "Failed" : "Queued";
        campaign.QueuedAt = campaign.QueuedCount > 0 ? timeProvider.GetUtcNow().UtcDateTime : null;
        campaign.SentCount = logs.Count(item => item.Status == "Sent");
        campaign.SentAt = campaign.SentCount == campaign.TotalRecipients && campaign.TotalRecipients > 0
            ? timeProvider.GetUtcNow().UtcDateTime
            : null;
        campaign.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    private string BuildCampaignBody(string body, string unsubscribeToken)
    {
        var url = $"{normalizedPublicBaseUrl}/newsletter/unsubscribe?token={Uri.EscapeDataString(unsubscribeToken)}";
        return $"{body}<hr><p style=\"font-size:12px;color:#666\">Bu e-postayı Hızlı Okuma bültenine abone olduğunuz için aldınız. "
            + $"<a href=\"{HtmlEncoder.Default.Encode(url)}\">Abonelikten çık</a></p>";
    }

    private static EmailCampaignSummary ToSummary(LegacyEmailCampaign row) => new(
        row.Id,
        row.Name,
        row.Subject,
        StatusValue(row.Status),
        row.TargetRoles,
        row.TargetInstitutionId,
        row.IncludeAllUsers,
        row.IncludeSubscribers,
        row.ScheduledFor,
        row.QueuedAt,
        row.SentAt,
        row.TotalRecipients,
        row.QueuedCount,
        row.SentCount,
        row.FailedCount,
        row.OpenedCount,
        row.ClickedCount,
        row.CreatedAt);

    private static int StatusValue(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "scheduled" => 1,
        "sending" or "queued" => 2,
        "sent" => 3,
        "cancelled" => 4,
        "failed" => 5,
        _ => 0
    };

    private static string? StatusName(int status) => status switch
    {
        0 => "Draft",
        1 => "Scheduled",
        2 => "Queued",
        3 => "Sent",
        4 => "Cancelled",
        5 => "Failed",
        _ => null
    };

    private static DateTime? NormalizeSchedule(DateTime? scheduledFor)
    {
        if (!scheduledFor.HasValue) return null;
        return scheduledFor.Value.Kind switch
        {
            DateTimeKind.Utc => scheduledFor.Value,
            DateTimeKind.Local => scheduledFor.Value.ToUniversalTime(),
            _ => throw new ArgumentException("Scheduled campaign time must include a timezone and be sent in UTC.", nameof(scheduledFor))
        };
    }

    private static void ValidateAudience(string? targetRoles, Guid? targetInstitutionId, bool includeAllUsers, bool includeSubscribers)
    {
        if (!includeSubscribers || includeAllUsers || targetInstitutionId.HasValue || !string.IsNullOrWhiteSpace(targetRoles))
        {
            throw new ArgumentException("Newsletter campaigns can target only consented, confirmed newsletter subscribers.");
        }
    }

    private static void Validate(string name, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 500)
            throw new ArgumentException("Subject is required and must not exceed 500 characters.");
        if (string.IsNullOrWhiteSpace(body) || body.Length > 200_000)
            throw new ArgumentException("Campaign body is required and must not exceed 200,000 characters.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ValidateBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("PublicApp:BaseUrl must be an absolute HTTP(S) URL without a query or fragment.");
        }

        return uri.ToString().TrimEnd('/');
    }
}
