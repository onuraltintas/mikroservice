using System.Diagnostics.Metrics;

namespace CourseManagement.API;

public static class CourseManagementOperationMetrics
{
    public const string MeterName = "EduPlatform.CourseManagement";
    public const string LessonConflictRejectionsName = "course_management.lesson.conflict.rejections";
    public const string PaymentErrorsName = "course_management.payment.errors";
    public const string ImportErrorsName = "course_management.import.errors";
    public const string AttachmentCleanupQueueFailuresName = "course_management.attachment.cleanup.queue.failures";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> LessonConflictRejections = Meter.CreateCounter<long>(LessonConflictRejectionsName);
    private static readonly Counter<long> PaymentErrors = Meter.CreateCounter<long>(PaymentErrorsName);
    private static readonly Counter<long> ImportErrors = Meter.CreateCounter<long>(ImportErrorsName);
    private static readonly Counter<long> AttachmentCleanupQueueFailures =
        Meter.CreateCounter<long>(AttachmentCleanupQueueFailuresName);

    internal static void RecordLessonConflict(int statusCode) =>
        LessonConflictRejections.Add(1, new KeyValuePair<string, object?>("http.response.status_code", statusCode));

    internal static void RecordPaymentError(int statusCode) =>
        PaymentErrors.Add(1, new KeyValuePair<string, object?>("http.response.status_code", statusCode));

    internal static void RecordImportError(int statusCode) =>
        ImportErrors.Add(1, new KeyValuePair<string, object?>("http.response.status_code", statusCode));

    internal static void RecordAttachmentCleanupQueueFailure() => AttachmentCleanupQueueFailures.Add(1);
}
