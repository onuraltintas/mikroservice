namespace EduPlatform.Shared.Kernel.Results;

/// <summary>
/// Represents an error with a code and description
/// </summary>
public record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NullValue = new("Error.NullValue", "A null value was provided");
    
    public static Error NotFound(string entityName, object id) => 
        new($"{entityName}.NotFound", $"{entityName} with id '{id}' was not found");
    
    public static Error Validation(string propertyName, string message) => 
        new($"Validation.{propertyName}", message);
    
    public static Error Conflict(string message) => 
        new("Error.Conflict", message);
    
    public static Error Unauthorized(string message = "Unauthorized access") => 
        new("Error.Unauthorized", message);
    
    public static Error Forbidden(string message = "Access denied") => 
        new("Error.Forbidden", message);

    public static implicit operator string(Error error) => error.Code;
}

/// <summary>
/// Represents a collection of validation errors
/// </summary>
public sealed record ValidationError : Error
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationError(IReadOnlyDictionary<string, string[]> errors) 
        : base("Validation.Error", "One or more validation errors occurred")
    {
        Errors = errors;
    }
}
