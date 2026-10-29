namespace SpeedReading.Infrastructure.Persistence;

public sealed class SpeedReadingErasureExecutionReceipt
{
    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public int DeletedRecordCount { get; private set; }
    public DateTime CompletedAt { get; private set; }
    /// <summary>Review H2b: set only while paid records of the subject are kept (cleared to null otherwise).</summary>
    public Guid? SubjectUserId { get; private set; }
    public DateTime? FinancialRetentionUntil { get; private set; }
    public DateTime? FinancialAnonymizedAt { get; private set; }

    /// <summary>Paid records are kept unchanged for this long after erasure, then their personal fields are removed.</summary>
    public const int FinancialRetentionYears = 1;

    private SpeedReadingErasureExecutionReceipt() { }

    public static SpeedReadingErasureExecutionReceipt Complete(
        Guid requestId,
        int deletedRecordCount,
        DateTime completedAt,
        Guid? subjectUserId = null,
        int retainedFinancialRecordCount = 0)
    {
        if (requestId == Guid.Empty)
            throw new ArgumentException("Request is required.", nameof(requestId));
        if (deletedRecordCount < 0)
            throw new ArgumentOutOfRangeException(nameof(deletedRecordCount));
        if (completedAt.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Completion timestamp must be UTC.", nameof(completedAt));
        return new SpeedReadingErasureExecutionReceipt
        {
            Id = Guid.NewGuid(),
            RequestId = requestId,
            DeletedRecordCount = deletedRecordCount,
            CompletedAt = completedAt,
            SubjectUserId = retainedFinancialRecordCount > 0 ? subjectUserId : null,
            FinancialRetentionUntil = retainedFinancialRecordCount > 0 ? completedAt.AddYears(FinancialRetentionYears) : null
        };
    }

    public void MarkFinancialDataAnonymized(DateTime at)
    {
        FinancialAnonymizedAt = at;
        SubjectUserId = null;
    }
}
