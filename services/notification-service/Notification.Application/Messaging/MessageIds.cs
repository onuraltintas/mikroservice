using System.Security.Cryptography;
using System.Text;
using MassTransit;

namespace Notification.Application.Messaging;

/// <summary>Message ids for idempotent writes: the incoming one, or one derived from stable parts.</summary>
public static class MessageIds
{
    /// <summary>The broker message id; every consumer relies on it for deduplication.</summary>
    public static Guid Require<T>(ConsumeContext<T> context)
        where T : class
        => context.MessageId ?? throw new InvalidOperationException($"{typeof(T).Name}.MessageId is required.");

    /// <summary>A deterministic id: the first 16 bytes of SHA-256 over the UTF-8 text.</summary>
    public static Guid Derive(string canonical) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)).AsSpan(0, 16));
}
