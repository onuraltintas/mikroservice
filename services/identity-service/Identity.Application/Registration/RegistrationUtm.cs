using System.Text;

namespace Identity.Application.Registration;

/// <summary>
/// Roadmap O11: the campaign tags (utm_source, utm_medium, utm_campaign) of the page a visitor landed on before
/// registering. Kept with the new account so sign-ups can be attributed without analytics cookies.
/// </summary>
public sealed record RegistrationUtm(string? UtmSource, string? UtmMedium, string? UtmCampaign)
{
    public const int MaxLength = 100;

    /// <summary>Lower-case ASCII letters, digits, '.', '_' and '-'; spaces become '-'; empty tags are dropped. Null when nothing is left.</summary>
    public static RegistrationUtm? Normalize(RegistrationUtm? utm)
    {
        if (utm is null) return null;
        var normalized = new RegistrationUtm(Clean(utm.UtmSource), Clean(utm.UtmMedium), Clean(utm.UtmCampaign));
        return normalized is { UtmSource: null, UtmMedium: null, UtmCampaign: null } ? null : normalized;
    }

    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var builder = new StringBuilder(Math.Min(value.Length, MaxLength));
        foreach (var raw in value.Trim().ToLowerInvariant())
        {
            var c = raw switch { 'ç' => 'c', 'ğ' => 'g', 'ı' => 'i', 'i' => 'i', 'ö' => 'o', 'ş' => 's', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u', _ => raw };
            if (char.IsWhiteSpace(c)) c = '-';
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.' or '_' or '-') builder.Append(c);
            if (builder.Length == MaxLength) break;
        }
        var text = builder.ToString().Trim('-');
        return text.Length == 0 ? null : text;
    }
}

public sealed record RegistrationSourceRow(string? UtmSource, string? UtmMedium, string? UtmCampaign, int Count);

/// <summary>Self-registrations of a product in a period, with the tagged ones grouped by campaign.</summary>
public sealed record RegistrationSourceSummary(DateTimeOffset From, DateTimeOffset To, int TotalRegistrations, int Untagged,
    IReadOnlyList<RegistrationSourceRow> Rows);
