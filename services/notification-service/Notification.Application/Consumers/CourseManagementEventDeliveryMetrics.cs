using System.Diagnostics.Metrics;

namespace Notification.Application.Consumers;

public static class CourseManagementEventDeliveryMetrics
{
    public const string MeterName = "EduPlatform.CourseManagement";
    public const string DeliveryDelayName = "course_management.event.delivery.delay";

    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Histogram<double> DeliveryDelay = Meter.CreateHistogram<double>(DeliveryDelayName, "ms");

    public static void RecordDeliveryDelay(DateTime? sentTimeUtc)
    {
        if (sentTimeUtc is null)
            return;

        var sentAt = new DateTimeOffset(DateTime.SpecifyKind(sentTimeUtc.Value, DateTimeKind.Utc));
        RecordDeliveryDelay(sentAt, DateTimeOffset.UtcNow);
    }

    public static void RecordDeliveryDelay(DateTimeOffset sentAt, DateTimeOffset receivedAt)
    {
        var delayMilliseconds = (receivedAt - sentAt).TotalMilliseconds;
        if (double.IsFinite(delayMilliseconds) && delayMilliseconds >= 0)
            DeliveryDelay.Record(delayMilliseconds);
    }
}
