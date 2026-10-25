namespace EduPlatform.Shared.Kernel.Exceptions;

/// <summary>
/// Base exception for all domain exceptions
/// </summary>
public abstract class DomainException : Exception
{
    public string Code { get; }

    protected DomainException(string code, string message) : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Exception thrown when an entity is not found
/// </summary>
public sealed class NotFoundException : DomainException
{
    public NotFoundException(string entityName, object id) 
        : base($"{entityName}.NotFound", $"{entityName} with id '{id}' was not found")
    {
    }
}

/// <summary>
/// Exception thrown when a business rule is violated
/// </summary>
public sealed class BusinessRuleException : DomainException
{
    public BusinessRuleException(string code, string message) 
        : base(code, message)
    {
    }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IReadOnlyDictionary<string, string[]> errors) 
        : base("Validation.Error", "One or more validation errors occurred")
    {
        Errors = errors;
    }
}

/// <summary>
/// Exception thrown when there's a concurrency conflict
/// </summary>
public sealed class ConcurrencyException : DomainException
{
    public ConcurrencyException(string entityName, object id) 
        : base("Concurrency.Conflict", $"Concurrency conflict for {entityName} with id '{id}'")
    {
    }
}
