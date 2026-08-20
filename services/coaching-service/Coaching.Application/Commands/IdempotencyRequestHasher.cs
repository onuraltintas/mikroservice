using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Coaching.Application.Idempotency;

internal static class IdempotencyRequestHasher
{
    public static string Create(params string?[] values)
    {
        var canonical = new StringBuilder();
        foreach (var value in values)
        {
            if (value is null)
            {
                canonical.Append("-1:");
                continue;
            }

            canonical.Append(value.Length).Append(':').Append(value);
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    public static string Format(Guid value) => value.ToString("D");

    public static string? Format(Guid? value) => value?.ToString("D");

    public static string Format(DateTime value) =>
        value.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);

    public static string? Format(decimal? value) =>
        value?.ToString(CultureInfo.InvariantCulture);

    public static string Format(IEnumerable<Guid> values) =>
        string.Join(",", values
            .Distinct()
            .OrderBy(value => value)
            .Select(Format));

    public static string? Format(IReadOnlyDictionary<string, decimal>? values) =>
        values is null
            ? null
            : string.Join(",", values
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key.Length}:{pair.Key}={pair.Value.ToString(CultureInfo.InvariantCulture)}"));
}
