using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>Roadmap O3: who the teacher writes with — the student, or a parent about the student.</summary>
public enum CoachingConversationKind
{
    TeacherStudent = 1,
    TeacherParent = 2
}

/// <summary>
/// Roadmap O3: a one-to-one conversation between a teacher and a counterpart (the student, or a parent of the student).
/// One conversation per teacher, counterpart and student. Read receipts are the last time each side opened it.
/// </summary>
public sealed class CoachingConversation : Entity
{
    public Guid TeacherUserId { get; private set; }
    public Guid StudentUserId { get; private set; }
    public Guid CounterpartUserId { get; private set; }
    public CoachingConversationKind Kind { get; private set; }
    /// <summary>The institution of the teacher–student link; null for an independent coach. Only that institution may review it.</summary>
    public Guid? InstitutionId { get; private set; }
    public DateTime LastMessageAt { get; private set; }
    public DateTime? TeacherReadAt { get; private set; }
    public DateTime? CounterpartReadAt { get; private set; }
    public int MessageCount { get; private set; }

    private CoachingConversation() { }

    public static CoachingConversation Create(Guid teacherUserId, Guid studentUserId, Guid counterpartUserId,
        CoachingConversationKind kind, Guid? institutionId, DateTime now)
    {
        if (teacherUserId == Guid.Empty || studentUserId == Guid.Empty || counterpartUserId == Guid.Empty || institutionId == Guid.Empty
            || teacherUserId == counterpartUserId || teacherUserId == studentUserId
            || kind == CoachingConversationKind.TeacherStudent && counterpartUserId != studentUserId
            || kind == CoachingConversationKind.TeacherParent && counterpartUserId == studentUserId
            || !Enum.IsDefined(kind) || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Geçersiz yazışma tarafları.");
        return new() { TeacherUserId = teacherUserId, StudentUserId = studentUserId, CounterpartUserId = counterpartUserId,
            Kind = kind, InstitutionId = institutionId, LastMessageAt = now, CreatedAt = now };
    }

    public bool IsParticipant(Guid userId) => userId == TeacherUserId || userId == CounterpartUserId;

    public Guid OtherParticipant(Guid userId) => userId == TeacherUserId ? CounterpartUserId
        : userId == CounterpartUserId ? TeacherUserId : throw new InvalidOperationException("Not a participant.");

    public DateTime? ReadAtOf(Guid userId) => userId == TeacherUserId ? TeacherReadAt : userId == CounterpartUserId ? CounterpartReadAt : null;

    public void MarkRead(Guid userId, DateTime now)
    {
        if (userId == TeacherUserId) TeacherReadAt = now;
        else if (userId == CounterpartUserId) CounterpartReadAt = now;
        else throw new InvalidOperationException("Not a participant.");
    }

    public void Record(CoachingMessage message, Guid? institutionId)
    {
        if (message.ConversationId != Id || !IsParticipant(message.SenderUserId)) throw new InvalidOperationException("Message does not belong here.");
        LastMessageAt = message.SentAt; MessageCount++; InstitutionId = institutionId;
        MarkRead(message.SenderUserId, message.SentAt);
        UpdatedAt = message.SentAt;
    }
}

public sealed class CoachingMessage : Entity
{
    public const int MaxBodyLength = 4000;
    public Guid ConversationId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; }

    private CoachingMessage() { }

    public static CoachingMessage Create(Guid conversationId, Guid senderUserId, string? body, bool hasAttachments, DateTime now)
    {
        var text = (body ?? string.Empty).Trim();
        if (text.Length == 0 && !hasAttachments) throw new ArgumentException("Mesaj yazın ya da dosya ekleyin.");
        if (text.Length > MaxBodyLength) throw new ArgumentException($"Mesaj en fazla {MaxBodyLength} karakter olabilir.");
        if (text.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t'))) throw new ArgumentException("Mesajda geçersiz karakter var.");
        if (conversationId == Guid.Empty || senderUserId == Guid.Empty || now.Kind != DateTimeKind.Utc) throw new ArgumentException("Geçersiz mesaj.");
        return new() { ConversationId = conversationId, SenderUserId = senderUserId, Body = text, SentAt = now, CreatedAt = now };
    }
}

/// <summary>A file uploaded into a conversation. It stays unattached (MessageId null) until it is sent with a message.</summary>
public sealed class CoachingMessageAttachment : Entity
{
    public Guid ConversationId { get; private set; }
    public Guid? MessageId { get; private set; }
    public Guid UploaderUserId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;

    private CoachingMessageAttachment() { }

    public static string KeyFor(Guid conversationId, Guid attachmentId) => $"messages/{conversationId:N}/{attachmentId:N}";

    public static CoachingMessageAttachment Create(Guid id, Guid conversationId, Guid uploaderUserId, string fileName,
        string contentType, long sizeBytes, string sha256, DateTime now) => new()
    {
        Id = id, ConversationId = conversationId, UploaderUserId = uploaderUserId, FileName = fileName, ContentType = contentType,
        SizeBytes = sizeBytes, Sha256 = sha256, StorageKey = KeyFor(conversationId, id), CreatedAt = now
    };

    public void AttachTo(Guid messageId)
    {
        if (MessageId.HasValue) throw new InvalidOperationException("The file was already sent.");
        MessageId = messageId;
    }
}

/// <summary>A recorded institution review of a conversation: who, when and why. Both participants can see that it happened.</summary>
public sealed class CoachingConversationAccess : Entity
{
    public Guid ConversationId { get; private set; }
    public Guid ViewerUserId { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTime AccessedAt { get; private set; }

    private CoachingConversationAccess() { }

    public static CoachingConversationAccess Create(Guid conversationId, Guid viewerUserId, string? reason, DateTime now)
    {
        var text = (reason ?? string.Empty).Trim();
        if (text.Length is < 10 or > 500) throw new ArgumentException("İnceleme gerekçesi 10–500 karakter olmalıdır.");
        if (text.Any(char.IsControl)) throw new ArgumentException("Gerekçede geçersiz karakter var.");
        return new() { ConversationId = conversationId, ViewerUserId = viewerUserId, Reason = text, AccessedAt = now, CreatedAt = now };
    }
}
