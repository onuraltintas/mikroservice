using EduPlatform.Shared.Kernel.Primitives;

namespace Identity.Domain.Entities;

/// <summary>
/// Stores the result identity of a retriable write owned by Identity.
/// The scope keeps keys from different commands independent.
/// </summary>
public sealed class IdempotencyRecord : Entity
{
    public string Scope { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string RequestHash { get; private set; } = string.Empty;
    public Guid ResourceId { get; private set; }

    private IdempotencyRecord() { }

    public static IdempotencyRecord Create(
        string scope,
        string key,
        string requestHash,
        Guid resourceId)
    {
        return new IdempotencyRecord
        {
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResourceId = resourceId,
            CreatedAt = DateTime.UtcNow
        };
    }

    public bool Matches(string requestHash) =>
        string.Equals(RequestHash, requestHash, StringComparison.Ordinal);
}
