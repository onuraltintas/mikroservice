namespace Identity.Application.Exceptions;

public sealed class IdempotencyConflictException(Exception innerException)
    : Exception("The idempotency key was claimed concurrently.", innerException);
