using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

public sealed class DataSubjectRequestExecutionResult : Entity
{
    public Guid RequestId { get; private set; }
    public string ServiceName { get; private set; } = string.Empty;
    public int DeletedRecordCount { get; private set; }
    public DateTime CompletedAt { get; private set; }

    private DataSubjectRequestExecutionResult() { }

    public static DataSubjectRequestExecutionResult Record(
        PersonalDataErasureExecutionCompletedV1 message)
    {
        if (message.RequestId == Guid.Empty)
            throw new ArgumentException("Request is required.", nameof(message));
        if (string.IsNullOrWhiteSpace(message.ServiceName) || message.ServiceName.Trim().Length > 100)
            throw new ArgumentException("Service name is required and must not exceed 100 characters.", nameof(message));
        if (message.DeletedRecordCount < 0)
            throw new ArgumentOutOfRangeException(nameof(message));
        if (message.CompletedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Completion timestamp must be UTC.", nameof(message));

        return new DataSubjectRequestExecutionResult
        {
            RequestId = message.RequestId,
            ServiceName = message.ServiceName.Trim(),
            DeletedRecordCount = message.DeletedRecordCount,
            CompletedAt = message.CompletedAt,
            CreatedAt = DateTime.UtcNow
        };
    }
}
