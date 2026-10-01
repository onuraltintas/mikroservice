namespace Identity.Application.Exceptions;

public sealed class IdentityConcurrencyConflictException(string message, Exception innerException)
    : Exception(message, innerException)
{
}
