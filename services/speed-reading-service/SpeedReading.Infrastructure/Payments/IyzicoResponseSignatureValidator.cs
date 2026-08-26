using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace SpeedReading.Infrastructure.Payments;

public static class IyzicoResponseSignatureValidator
{
    public static bool ValidateInitialization(
        string? conversationId,
        string? token,
        string? signature,
        string secretKey)
    {
        if (string.IsNullOrWhiteSpace(conversationId)
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        return Matches(string.Join(':', conversationId, token), signature, secretKey);
    }

    public static bool ValidateRetrieve(
        string? paymentStatus,
        string? paymentId,
        string? currency,
        string? basketId,
        string? conversationId,
        decimal? paidPrice,
        decimal? price,
        string? token,
        string? signature,
        string secretKey)
    {
        if (string.IsNullOrWhiteSpace(paymentStatus)
            || string.IsNullOrWhiteSpace(paymentId)
            || string.IsNullOrWhiteSpace(currency)
            || string.IsNullOrWhiteSpace(basketId)
            || string.IsNullOrWhiteSpace(conversationId)
            || !paidPrice.HasValue
            || !price.HasValue
            || string.IsNullOrWhiteSpace(token)
            || string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var data = string.Join(':',
            paymentStatus,
            paymentId,
            currency,
            basketId,
            conversationId,
            FormatAmount(paidPrice.Value),
            FormatAmount(price.Value),
            token);
        return Matches(data, signature, secretKey);
    }

    private static bool Matches(string data, string suppliedSignature, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var expected = Encoding.ASCII.GetBytes(
            Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant());
        var supplied = Encoding.ASCII.GetBytes(suppliedSignature.Trim().ToLowerInvariant());
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private static string FormatAmount(decimal value) =>
        value.ToString("0.###############################", CultureInfo.InvariantCulture);
}
