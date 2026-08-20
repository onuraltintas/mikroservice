using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>
/// Stores the resource identity of a retriable write owned by Coaching.
/// Keys are isolated by command scope and the request hash prevents accidental reuse.
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
