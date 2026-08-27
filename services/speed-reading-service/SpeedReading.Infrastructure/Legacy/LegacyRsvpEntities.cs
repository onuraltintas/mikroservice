namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacyRsvpSession : LegacyBaseEntity
{
    public Guid UserId { get; set; }
    public Guid? TextId { get; set; }
    public string? TextContent { get; set; }
    public int WordsPerMinute { get; set; }
    public decimal SourceAverageWpm { get; set; }
    public string FontFamily { get; set; } = "Arial";
    public int FontSize { get; set; }
    public string BackgroundColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#000000";
    public int TotalWords { get; set; }
    public int CompletedWords { get; set; }
    public decimal CompletionPercentage { get; set; }
    public int SessionDuration { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAt { get; set; }

    // Source-only RSVP fields are intentionally not part of the owned model;
    // the source mapping projects them into the compatible fields above.
}
