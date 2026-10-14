namespace CourseManagement.Domain;

public sealed class CourseManagementErasureExecution
{
    private CourseManagementErasureExecution() { }

    public Guid RequestId { get; private set; }
    public int DeletedRecordCount { get; private set; }
    public DateTimeOffset CompletedAt { get; private set; }

    public static CourseManagementErasureExecution Complete(Guid requestId, int deletedRecordCount,
        DateTimeOffset completedAt)
    {
        if (requestId == Guid.Empty || deletedRecordCount < 0 || completedAt == default)
            throw new ArgumentException("Erasure execution details are invalid.");
        return new CourseManagementErasureExecution
        {
            RequestId = requestId,
            DeletedRecordCount = deletedRecordCount,
            CompletedAt = completedAt.ToUniversalTime()
        };
    }
}
