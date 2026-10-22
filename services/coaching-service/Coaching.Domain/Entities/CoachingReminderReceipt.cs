using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public sealed class CoachingReminderReceipt : Entity
{
    public Guid RecipientUserId { get; private set; }
    public Guid SourceId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public string OccurrenceKey { get; private set; } = string.Empty;
    public DateTime DueAt { get; private set; }
    private CoachingReminderReceipt() { }

    public static CoachingReminderReceipt Create(Guid recipient, Guid source, string kind, string occurrenceKey, DateTime dueAt, DateTime queuedAt)
    {
        if (recipient == Guid.Empty || source == Guid.Empty || kind is not ("StudyDay" or "StudyOverdue" or "Session" or "FeeOverdue") || dueAt.Kind != DateTimeKind.Utc || queuedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A reminder requires valid ownership, kind and UTC dates.");
        var occurrence = StudyCatalogText.Require(occurrenceKey, 64);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"coaching-reminder:{kind}:{source:N}:{recipient:N}:{occurrence}"));
        return new() { Id = new Guid(hash.AsSpan(0, 16)), RecipientUserId = recipient, SourceId = source, Kind = kind,
            OccurrenceKey = occurrence, DueAt = dueAt, CreatedAt = queuedAt };
    }
}
