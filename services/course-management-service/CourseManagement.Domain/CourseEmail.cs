using System.Net.Mail;
using System.Text.Json;

namespace CourseManagement.Domain;

/// <summary>The e-mails an institution can send to students and guardians (19.11.2026: e-mail only, no portal).</summary>
public enum CourseEmailKind
{
    Absence = 1,
    HomeworkNotDone = 2,
    InstallmentReminder = 3,
    InstallmentOverdue = 4,
    LessonChanged = 5,
    WeeklySchedule = 6,
    PaymentReceived = 7
}

/// <summary>Who receives a kind: the student's guardians, the student, or both.</summary>
public enum CourseEmailAudience
{
    Guardians = 1,
    Student = 2,
    Both = 3
}

public sealed record CourseEmailRule(CourseEmailKind Kind, bool Enabled, CourseEmailAudience Audience, string? Note);

/// <summary>
/// An institution's e-mail choices. Every kind starts switched off; switching one on needs the owner's statement that
/// students and guardians were informed (the institution is the data controller).
/// </summary>
public sealed class WorkspaceEmailSettings
{
    public const int MaxNoteLength = 300;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private WorkspaceEmailSettings() { }

    public Guid WorkspaceId { get; private set; }
    public string RulesJson { get; private set; } = "[]";
    public int InstallmentDaysBefore { get; private set; } = 3;
    public bool InstallmentOnDueDay { get; private set; } = true;
    public int OverdueDaysAfter { get; private set; } = 3;
    public int SendHour { get; private set; } = 9;
    public string? ReplyToEmail { get; private set; }
    public DateTimeOffset? InformedConsentAt { get; private set; }
    public Guid? InformedConsentByUserId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static WorkspaceEmailSettings CreateDefault(Guid workspaceId, DateTimeOffset now)
    {
        if (workspaceId == Guid.Empty) throw new ArgumentException("Workspace is required.", nameof(workspaceId));
        return new WorkspaceEmailSettings { WorkspaceId = workspaceId, UpdatedAt = now.ToUniversalTime() };
    }

    public IReadOnlyList<CourseEmailRule> Rules
    {
        get
        {
            var stored = JsonSerializer.Deserialize<CourseEmailRule[]>(RulesJson, Json) ?? [];
            return Enum.GetValues<CourseEmailKind>().Select(kind => stored.FirstOrDefault(rule => rule.Kind == kind)
                ?? new CourseEmailRule(kind, false, DefaultAudience(kind), null)).ToArray();
        }
    }

    public CourseEmailRule Rule(CourseEmailKind kind) => Rules.Single(rule => rule.Kind == kind);

    public bool IsEnabled(CourseEmailKind kind) => InformedConsentAt is not null && Rule(kind).Enabled;

    public static CourseEmailAudience DefaultAudience(CourseEmailKind kind) => kind switch
    {
        CourseEmailKind.LessonChanged or CourseEmailKind.WeeklySchedule => CourseEmailAudience.Both,
        _ => CourseEmailAudience.Guardians
    };

    public void Update(IReadOnlyCollection<CourseEmailRule> rules, int installmentDaysBefore, bool installmentOnDueDay,
        int overdueDaysAfter, int sendHour, string? replyToEmail, bool informedConsentConfirmed, Guid userId, DateTimeOffset now)
    {
        if (rules.Select(rule => rule.Kind).Distinct().Count() != rules.Count
            || rules.Any(rule => !Enum.IsDefined(rule.Kind) || !Enum.IsDefined(rule.Audience)))
            throw new ArgumentException("Each e-mail kind may appear once with a known audience.", nameof(rules));
        if (installmentDaysBefore is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(installmentDaysBefore));
        if (overdueDaysAfter is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(overdueDaysAfter));
        if (sendHour is < 7 or > 21) throw new ArgumentOutOfRangeException(nameof(sendHour));
        var normalizedRules = rules.Select(rule => rule with { Note = NormalizeNote(rule.Note) }).OrderBy(rule => rule.Kind).ToArray();
        var anyEnabled = normalizedRules.Any(rule => rule.Enabled);
        if (anyEnabled && !informedConsentConfirmed && InformedConsentAt is null)
            throw new InvalidOperationException("Informed consent must be confirmed before e-mails are switched on.");

        RulesJson = JsonSerializer.Serialize(normalizedRules, Json);
        InstallmentDaysBefore = installmentDaysBefore;
        InstallmentOnDueDay = installmentOnDueDay;
        OverdueDaysAfter = overdueDaysAfter;
        SendHour = sendHour;
        ReplyToEmail = NormalizeEmail(replyToEmail);
        if (informedConsentConfirmed && InformedConsentAt is null)
        {
            InformedConsentAt = now.ToUniversalTime();
            InformedConsentByUserId = userId;
        }
        UpdatedAt = now.ToUniversalTime();
    }

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        var value = note.Trim();
        if (value.Length > MaxNoteLength) throw new ArgumentException($"A note cannot exceed {MaxNoteLength} characters.", nameof(note));
        return value;
    }

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var value = email.Trim().ToLowerInvariant();
        if (value.Length > 320 || !MailAddress.TryCreate(value, out var parsed) || parsed.Address != value || !value.Contains('.'))
            throw new ArgumentException("A valid e-mail address is required.", nameof(email));
        return value;
    }
}

public enum CourseEmailStatus
{
    Pending = 1,
    Sent = 2,
    Skipped = 3
}

/// <summary>
/// One e-mail about one student, waiting for its moment and then kept as the institution's sending record.
/// <see cref="DedupKey"/> makes it once per student and day (absence, homework), per change, installment or week.
/// Waiting a little lets a corrected mark or a reversed payment cancel it before anyone is told.
/// </summary>
public sealed class CourseEmail
{
    public const int MaxRecipientsLength = 2000;

    private CourseEmail() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public CourseEmailKind Kind { get; private set; }
    public Guid StudentId { get; private set; }
    public string DedupKey { get; private set; } = string.Empty;
    public string? ReferencesJson { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public CourseEmailStatus Status { get; private set; }
    public string? Subject { get; private set; }
    public string? Recipients { get; private set; }
    public int RecipientCount { get; private set; }
    public string? SkipReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }

    public static CourseEmail Create(Guid workspaceId, CourseEmailKind kind, Guid studentId, string dedupKey,
        DateTimeOffset dueAt, DateTimeOffset now, IEnumerable<Guid>? references = null)
    {
        if (workspaceId == Guid.Empty || studentId == Guid.Empty || !Enum.IsDefined(kind))
            throw new ArgumentException("Workspace, student and kind are required.");
        if (string.IsNullOrWhiteSpace(dedupKey) || dedupKey.Length > 200) throw new ArgumentException("A key is required.", nameof(dedupKey));
        var email = new CourseEmail
        {
            Id = Guid.NewGuid(), WorkspaceId = workspaceId, Kind = kind, StudentId = studentId, DedupKey = dedupKey,
            DueAt = dueAt.ToUniversalTime(), Status = CourseEmailStatus.Pending, CreatedAt = now.ToUniversalTime()
        };
        if (references is not null) email.AddReferences(references);
        return email;
    }

    public IReadOnlyList<Guid> References =>
        ReferencesJson is null ? [] : JsonSerializer.Deserialize<Guid[]>(ReferencesJson) ?? [];

    /// <summary>Adds what the e-mail is about (lessons, assignments) while it still waits.</summary>
    public void AddReferences(IEnumerable<Guid> references)
    {
        if (Status != CourseEmailStatus.Pending) return;
        var merged = References.Concat(references).Where(id => id != Guid.Empty).Distinct().Take(50).ToArray();
        ReferencesJson = merged.Length == 0 ? null : JsonSerializer.Serialize(merged);
    }

    public void Postpone(DateTimeOffset dueAt)
    {
        if (Status == CourseEmailStatus.Pending && dueAt > DueAt) DueAt = dueAt.ToUniversalTime();
    }

    public void MarkSent(string subject, IReadOnlyCollection<string> recipients, DateTimeOffset now)
    {
        if (Status != CourseEmailStatus.Pending) throw new InvalidOperationException("Only a waiting e-mail can be sent.");
        if (recipients.Count == 0) throw new ArgumentException("At least one recipient is required.", nameof(recipients));
        Status = CourseEmailStatus.Sent;
        Subject = subject.Length > 200 ? subject[..200] : subject;
        var joined = string.Join(", ", recipients);
        Recipients = joined.Length > MaxRecipientsLength ? joined[..MaxRecipientsLength] : joined;
        RecipientCount = recipients.Count;
        ProcessedAt = now.ToUniversalTime();
    }

    public void Skip(string reason, DateTimeOffset now)
    {
        if (Status != CourseEmailStatus.Pending) return;
        Status = CourseEmailStatus.Skipped;
        SkipReason = reason.Length > 100 ? reason[..100] : reason;
        ProcessedAt = now.ToUniversalTime();
    }
}

/// <summary>An address that asked not to receive an institution's e-mails. Only the address holder can undo it.</summary>
public sealed class CourseEmailOptOut
{
    private CourseEmailOptOut() { }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    public static CourseEmailOptOut Create(Guid workspaceId, string email, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = workspaceId == Guid.Empty ? throw new ArgumentException("Workspace is required.") : workspaceId,
        Email = WorkspaceEmailSettings.NormalizeEmail(email) ?? throw new ArgumentException("E-mail is required."),
        CreatedAt = now.ToUniversalTime()
    };
}
