using System.Security.Cryptography;
using System.Text;

namespace SpeedReading.Infrastructure.Payments;

public static class IyzicoRequestSigner
{
    public static string CreateAuthorization(
        string apiKey,
        string secretKey,
        string uriPath,
        string requestBody,
        string randomKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(secretKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(uriPath);
        ArgumentNullException.ThrowIfNull(requestBody);
        ArgumentException.ThrowIfNullOrWhiteSpace(randomKey);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        var payload = randomKey + uriPath + requestBody;
        var signature = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var authorizationData = $"apiKey:{apiKey}&randomKey:{randomKey}&signature:{signature}";

        return $"IYZWSv2 {Convert.ToBase64String(Encoding.UTF8.GetBytes(authorizationData))}";
    }
}
