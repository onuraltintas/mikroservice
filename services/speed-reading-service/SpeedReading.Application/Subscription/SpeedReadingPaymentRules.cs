namespace SpeedReading.Application.Subscription;

public static class SpeedReadingPaymentRules
{
    public const string PendingStatus = "Pending";
    public const string SuccessStatus = "Success";
    public const string FailedStatus = "Failed";

    public static bool IsSuccessful(string? paymentStatus, int? fraudStatus) =>
        string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase)
        && fraudStatus == 1;

    public static string ResolveStatus(string? paymentStatus, int? fraudStatus)
    {
        if (IsSuccessful(paymentStatus, fraudStatus))
        {
            return SuccessStatus;
        }

        if (string.Equals(paymentStatus, "FAILURE", StringComparison.OrdinalIgnoreCase)
            || fraudStatus == -1)
        {
            return FailedStatus;
        }

        return PendingStatus;
    }

    public static DateTime? ResolveEndDate(DateTime startDate, int? durationDays) =>
        durationDays is > 0 ? startDate.AddDays(durationDays.Value) : null;

    public static bool IsValidIdentityNumber(string? identityNumber) =>
        !string.IsNullOrWhiteSpace(identityNumber)
        && identityNumber.Length == 11
        && identityNumber.All(char.IsDigit);
}
