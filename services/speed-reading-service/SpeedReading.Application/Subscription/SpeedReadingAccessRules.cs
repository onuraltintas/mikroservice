namespace SpeedReading.Application.Subscription;

public static class SpeedReadingAccessRules
{
    public static DateTime? ResolveEndDate(
        DateTime startDate,
        DateTime? overrideEndDate,
        int? planDurationDays) =>
        overrideEndDate ?? SpeedReadingPaymentRules.ResolveEndDate(startDate, planDurationDays);
}
