using Microsoft.EntityFrameworkCore;
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

internal sealed class LegacySpeedReadingEmailCampaigns(ISpeedReadingDataContext db) : ISpeedReadingEmailCampaigns
{
    public async Task<IReadOnlyList<EmailCampaignSummary>> GetAllAsync(int? status, CancellationToken cancellationToken)
    {
        var query = db.EmailCampaigns
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        var statusName = status.HasValue ? StatusName(status.Value) : null;
        if (statusName is not null) query = query.Where(item => item.Status == statusName);

        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<EmailCampaignDetail?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return null;

        var logs = await db.EmailCampaignLogs
            .AsNoTracking()
            .Where(item => item.CampaignId == id && !item.IsDeleted)
            .OrderByDescending(item => item.SentAt)
            .ThenByDescending(item => item.CreatedAt)
            .Take(100)
            .Select(item => new EmailCampaignLogSummary(
                item.Id,
                item.RecipientEmail,
                item.Status,
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
        var row = new LegacyEmailCampaign
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Subject = request.Subject.Trim(),
            Body = request.Body,
            PlainTextBody = Normalize(request.PlainTextBody),
            TargetRoles = Normalize(request.TargetRoles),
            TargetInstitutionId = request.TargetInstitutionId,
            IncludeAllUsers = request.IncludeAllUsers,
            IncludeSubscribers = request.IncludeSubscribers,
            ScheduledFor = request.ScheduledFor,
            Status = request.ScheduledFor.HasValue && request.ScheduledFor > DateTime.UtcNow ? "Scheduled" : "Draft",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };
        db.EmailCampaigns.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<bool> UpdateAsync(Guid id, UpdateEmailCampaignRequest request, CancellationToken cancellationToken)
    {
        Validate(request.Name, request.Subject, request.Body);
        var row = await db.EmailCampaigns
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        if (row.Status is "Sent" or "Sending")
            throw new InvalidOperationException("Cannot update a sent or sending campaign.");

        row.Name = request.Name.Trim();
        row.Subject = request.Subject.Trim();
        row.Body = request.Body;
        row.PlainTextBody = Normalize(request.PlainTextBody);
        row.TargetRoles = Normalize(request.TargetRoles);
        row.TargetInstitutionId = request.TargetInstitutionId;
        row.IncludeAllUsers = request.IncludeAllUsers;
        row.IncludeSubscribers = request.IncludeSubscribers;
        row.ScheduledFor = request.ScheduledFor;
        row.Status = request.ScheduledFor.HasValue && request.ScheduledFor > DateTime.UtcNow ? "Scheduled" : "Draft";
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return false;
        row.IsDeleted = true;
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<EmailCampaignSummary?> SendAsync(
        Guid id,
        SendEmailCampaignRequest request,
        CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return null;
        if (row.Status == "Sent") throw new InvalidOperationException("Campaign already sent.");

        if (!request.SendNow && row.ScheduledFor > DateTime.UtcNow)
        {
            row.Status = "Scheduled";
        }
        else
        {
            // The legacy endpoint did not have a queue/SMTP worker. Preserve
            // its state transition without inventing recipient statistics.
            row.Status = "Sending";
            row.SentAt = DateTime.UtcNow;
            row.Status = "Sent";
        }

        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToSummary(row);
    }

    public async Task<EmailCampaignStats?> GetStatsAsync(Guid id, CancellationToken cancellationToken)
    {
        var row = await db.EmailCampaigns
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (row is null) return null;

        return new EmailCampaignStats(
            row.TotalRecipients,
            row.SentCount,
            row.FailedCount,
            row.OpenedCount,
            row.ClickedCount,
            Math.Max(0, row.TotalRecipients - row.SentCount - row.FailedCount));
    }

    private static EmailCampaignSummary ToSummary(LegacyEmailCampaign row) =>
        new(
            row.Id,
            row.Name,
            row.Subject,
            StatusValue(row.Status),
            row.TargetRoles,
            row.TargetInstitutionId,
            row.IncludeAllUsers,
            row.IncludeSubscribers,
            row.ScheduledFor,
            row.SentAt,
            row.TotalRecipients,
            row.SentCount,
            row.FailedCount,
            row.OpenedCount,
            row.ClickedCount,
            row.CreatedAt);

    private static int StatusValue(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "scheduled" => 1,
        "sending" => 2,
        "sent" => 3,
        "cancelled" => 4,
        "failed" => 5,
        _ => 0
    };

    private static string? StatusName(int status) => status switch
    {
        0 => "Draft",
        1 => "Scheduled",
        2 => "Sending",
        3 => "Sent",
        4 => "Cancelled",
        5 => "Failed",
        _ => null
    };

    private static void Validate(string name, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(subject) || subject.Trim().Length > 500)
            throw new ArgumentException("Subject is required and must not exceed 500 characters.");
        if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("Body is required.");
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
