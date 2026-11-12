using System.Globalization;

namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Words Takistoskop never shows (approved November 2026), only for Takistoskop: Kelime Hazinesi keeps them.
/// Takistoskop measures quick recognition of familiar words, so its pools leave out emotion/colour words that
/// read as judgements, literary and music terms, internet and technology jargon, and loanwords. Matching is by
/// whole word after Turkish lower case, without folding the circumflex, so "Hikaye" does not drop "Hikâye".
/// Any word with an upper-case letter after the first one (acronyms such as USB, brand terms such as WiFi)
/// is dropped as well, also when new ones are added to the vocabulary later.
/// </summary>
public static class TachistoscopeWordExclusions
{
    public static readonly IReadOnlyList<string> Words =
    [
        "Bej", "Endişeli", "Korkmuş", "Kızgın", "Mat", "Çirkin", "Abonelik", "Adapte", "Akor", "Akım", "Alegori", "Analog",
        "Antivirüs", "Arp", "Banner", "Beğeni", "Blog", "Bluetooth", "Captcha", "Diatonik", "Dize", "Domain", "Dram", "Drone",
        "Emoji", "Epik", "GIF", "Global", "Hashtag", "Hikaye", "Influencer", "Kafiye", "Kaos", "Kaygı", "Kriz", "Laptop",
        "Link", "Lirik", "Lokal", "Miraz", "Mısra", "Nazım", "Nesir", "Not", "Ping", "Podcast", "Prestige", "Redif", "Reels",
        "Router", "Sansür", "Savaş", "Scooter", "Ses", "Siber", "Spam", "Takipçi", "Teşbih", "Ton", "Tweet", "URL", "USB",
        "Uyak", "Viral", "VPN", "WiFi", "Wiki", "Özçekim"
    ];

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly HashSet<string> Normalized = Words.Select(Normalize).ToHashSet(StringComparer.Ordinal);

    public static bool IsExcluded(string word)
    {
        var trimmed = word.Trim().Normalize();
        return trimmed.Skip(1).Any(char.IsUpper) || Normalized.Contains(Normalize(trimmed));
    }

    private static string Normalize(string word) => word.Trim().Normalize().ToLower(Turkish);
}
