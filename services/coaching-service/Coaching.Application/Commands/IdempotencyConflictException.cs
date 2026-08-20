namespace Coaching.Application.Exceptions;

/// <summary>
/// Raised when a concurrent request wins the idempotency key race.
/// </summary>
public sealed class IdempotencyConflictException(Exception innerException) : Exception(
    "An idempotency key was concurrently claimed.",
    innerException);
