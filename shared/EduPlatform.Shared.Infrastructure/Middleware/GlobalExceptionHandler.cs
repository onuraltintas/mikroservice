using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Shared.Infrastructure.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Exception occurred: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Instance = httpContext.Request.Path
        };

        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        switch (exception)
        {
            case ValidationException validationException:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Validation Error";
                problemDetails.Type = "https://eduplatform.dev/problems/validation-error";
                problemDetails.Detail = "One or more validation errors occurred.";
                problemDetails.Extensions["errors"] = validationException.Errors;
                break;

            case NotFoundException notFoundException:
                problemDetails.Status = StatusCodes.Status404NotFound;
                problemDetails.Title = "Not Found";
                problemDetails.Type = "https://eduplatform.dev/problems/not-found";
                problemDetails.Detail = notFoundException.Message;
                problemDetails.Extensions["code"] = notFoundException.Code;
                break;

            case BusinessRuleException businessRuleException:
                var isAuthorizationFailure = businessRuleException.Code.StartsWith(
                    "Authorization.", StringComparison.OrdinalIgnoreCase);
                problemDetails.Status = isAuthorizationFailure
                    ? StatusCodes.Status403Forbidden
                    : StatusCodes.Status400BadRequest;
                problemDetails.Title = isAuthorizationFailure
                    ? "Forbidden"
                    : "Business Rule Violation";
                problemDetails.Type = isAuthorizationFailure
                    ? "https://eduplatform.dev/problems/forbidden"
                    : "https://eduplatform.dev/problems/business-rule";
                problemDetails.Detail = businessRuleException.Message;
                problemDetails.Extensions["code"] = businessRuleException.Code;
                break;

            case ConcurrencyException concurrencyException:
                problemDetails.Status = StatusCodes.Status409Conflict;
                problemDetails.Title = "Concurrency Conflict";
                problemDetails.Type = "https://eduplatform.dev/problems/concurrency-conflict";
                problemDetails.Detail = concurrencyException.Message;
                problemDetails.Extensions["code"] = concurrencyException.Code;
                break;
                
            case DomainException domainException:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Domain Error";
                problemDetails.Type = "https://eduplatform.dev/problems/domain-error";
                problemDetails.Detail = domainException.Message;
                problemDetails.Extensions["code"] = domainException.Code;
                break;

            case ArgumentException argumentException:
                problemDetails.Status = StatusCodes.Status400BadRequest;
                problemDetails.Title = "Invalid Argument";
                problemDetails.Type = "https://eduplatform.dev/problems/invalid-argument";
                problemDetails.Detail = argumentException.Message;
                break;

            default:
                problemDetails.Status = StatusCodes.Status500InternalServerError;
                problemDetails.Title = "Internal Server Error";
                problemDetails.Type = "https://eduplatform.dev/problems/unexpected-error";
                problemDetails.Detail = "An unexpected error occurred.";
                break;
        }

        httpContext.Response.StatusCode = problemDetails.Status.Value;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response
            .WriteAsJsonAsync(
                problemDetails,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);

        return true;
    }
}
