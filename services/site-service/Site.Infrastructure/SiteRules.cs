using System.Text.RegularExpressions;

namespace Site.Infrastructure;

/// <summary>The kinds of content the sites are built from (eduivme.com and maxkampus.com).</summary>
public static class SiteKinds
{
    /// <summary>The site's own settings: footer text, the home page's search engine title and description.</summary>
    public const string Site = "site";
    /// <summary>The home page's opening block.</summary>
    public const string Hero = "hero";
    /// <summary>The heading and lead of a section (home: products, audiences, faq; page: features, audiences, steps, faq).</summary>
    public const string Section = "section";
    /// <summary>A product card on the home page.</summary>
    public const string Product = "product";
    /// <summary>"Kimler için" card (home page or product page).</summary>
    public const string Audience = "audience";
    /// <summary>A question and its answer (home page or product page).</summary>
    public const string Faq = "faq";
    /// <summary>A product page on eduivme.com (/urunler/{slug}), for a product without a site of its own.</summary>
    public const string Page = "page";
    /// <summary>A feature on a product page.</summary>
    public const string Feature = "feature";
    /// <summary>A "Nasıl başlanır" step on a product page.</summary>
    public const string Step = "step";
    /// <summary>A customer's words (maxkampus.com): body = the words, title = the person, subtitle = institution and city.</summary>
    public const string Review = "review";
    /// <summary>A blog post (maxkampus.com /blog/{slug}).</summary>
    public const string Post = "post";
    /// <summary>A menu link; category "header" or "footer".</summary>
    public const string Menu = "menu";
    /// <summary>A notice on the first page of the institution panel; audience all, owners or teachers.</summary>
    public const string Notice = "notice";
    /// <summary>What the pricing page says about a billing plan (slug = the plan's id); the price comes from billing.</summary>
    public const string Plan = "plan";

    public static readonly IReadOnlySet<string> HomeKinds = new HashSet<string> { Site, Hero, Section, Product, Audience, Faq, Page };
    public static readonly IReadOnlySet<string> PageKinds = new HashSet<string> { Section, Feature, Audience, Step, Faq };
    public static readonly IReadOnlySet<string> HomeSections = new HashSet<string> { "products", "audiences", "faq" };
    public static readonly IReadOnlySet<string> PageSections = new HashSet<string> { "features", "audiences", "steps", "faq" };

    /// <summary>maxkampus.com has no product pages: every entry sits on the site itself (empty group).</summary>
    public static readonly IReadOnlySet<string> MaxkampusKinds = new HashSet<string>
        { Site, Hero, Section, Feature, Step, Review, Faq, Page, Post, Menu, Notice, Plan };
    public static readonly IReadOnlySet<string> MaxkampusSections = new HashSet<string> { "features", "steps", "reviews", "faq", "pricing", "blog", "cta" };
    public static readonly IReadOnlySet<string> MenuPlaces = new HashSet<string> { "header", "footer" };
    public static readonly IReadOnlySet<string> NoticeAudiences = new HashSet<string> { "all", "owners", "teachers" };
}

public static partial class SiteRules
{
    public const int TitleLength = 200;
    public const int SubtitleLength = 500;
    public const int BodyLength = 4000;
    /// <summary>Blog posts and content pages are longer.</summary>
    public const int LongBodyLength = 40000;
    public const int MessageLength = 4000;

    public const string Eduivme = "eduivme";
    public const string Maxkampus = "maxkampus";
    public static readonly IReadOnlySet<string> Sites = new HashSet<string> { Eduivme, Maxkampus };
    public const int LabelLength = 80;
    public const int UrlLength = 500;
    public const int SeoTitleLength = 120;
    public const int MaxHighlights = 6;
    public const int HighlightLength = 160;
    public const int MaxMediaBytes = 2 * 1024 * 1024;

    public static readonly IReadOnlySet<string> Brands = new HashSet<string> { "eduivme", "maxkoc", "ho", "km" };

    /// <summary>The icons the site can draw; the admin picks one by name.</summary>
    public static readonly IReadOnlySet<string> Icons = new HashSet<string>
    {
        "calendar", "check", "assignment", "payment", "building", "person", "chart", "book", "clock", "chat", "star",
        "shield", "users", "target", "bolt", "school", "mail"
    };

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public static partial Regex SlugPattern();

    /// <summary>A link is an https address, a path on the site, or an anchor on the page. Nothing else is accepted.</summary>
    public static bool IsSafeUrl(string url)
    {
        if (url.StartsWith('#')) return url.Length > 1 && !url.Any(char.IsWhiteSpace);
        if (url.StartsWith('/')) return !url.StartsWith("//") && !url.Contains('\\') && !url.Any(char.IsWhiteSpace);
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);
    }
}

/// <summary>A request the service refuses; the endpoint turns it into a problem response.</summary>
public sealed class SiteRequestException(int status, string message, IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int Status { get; } = status;
    public IDictionary<string, string[]> Errors { get; } = errors ?? new Dictionary<string, string[]>();

    public static SiteRequestException NotFound(string message) => new(404, message);
    public static SiteRequestException Conflict(string message) => new(409, message);
}
