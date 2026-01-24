using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Events;

/// <summary>
/// Sınav sonucu eklendi event'i
/// </summary>
public record ExamResultAddedEvent(
    Guid ExamId,
    Guid StudentId,
    decimal Score,
    int? Ranking
) : DomainEvent
{
    public override string EventType => nameof(ExamResultAddedEvent);
}

/// <summary>
/// Sınav oluşturuldu event'i
/// </summary>
public record ExamCreatedEvent(
    Guid ExamId,
    Guid CreatedByTeacherId,
    string Title,
    DateTime ExamDate
) : DomainEvent
{
    public override string EventType => nameof(ExamCreatedEvent);
}
