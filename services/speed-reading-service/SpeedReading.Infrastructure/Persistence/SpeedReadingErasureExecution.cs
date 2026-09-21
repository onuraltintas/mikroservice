namespace SpeedReading.Infrastructure.Persistence;

public sealed class SpeedReadingErasureExecutionReceipt
{
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public int DeletedRecordCount { get; private set; }
    public DateTime CompletedAt { get; private set; }

    private SpeedReadingErasureExecutionReceipt() { }

    public static SpeedReadingErasureExecutionReceipt Complete(
        Guid requestId,
        int deletedRecordCount,
        DateTime completedAt)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request is required.", nameof(requestId));
        if (deletedRecordCount < 0)
            throw new ArgumentOutOfRangeException(nameof(deletedRecordCount));
        if (completedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Completion timestamp must be UTC.", nameof(completedAt));
        return new SpeedReadingErasureExecutionReceipt
        {
            Id = requestId,
            RequestId = requestId,
            DeletedRecordCount = deletedRecordCount,
            CompletedAt = completedAt
        };
    }
}
