namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedIdempotencyRecord
{
    public Guid Id { get; set; }
    public string Scope { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public Guid ResourceId { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool Matches(string requestHash) =>
        string.Equals(RequestHash, requestHash, StringComparison.Ordinal);
}
