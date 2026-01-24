using Mediator;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EduPlatform.Shared.Infrastructure.Behaviors;

/// <summary>
/// Logging behavior for all requests - logs request/response and execution time
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IMessage
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var requestId = Guid.NewGuid().ToString("N")[..8];

        _logger.LogInformation(
            "[{RequestId}] Starting {RequestName} {@Request}",
            requestId, requestName, request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(request, cancellationToken);
            
            stopwatch.Stop();
            
            _logger.LogInformation(
                "[{RequestId}] Completed {RequestName} in {ElapsedMs}ms",
                requestId, requestName, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "[{RequestId}] Failed {RequestName} after {ElapsedMs}ms - {ErrorMessage}",
                requestId, requestName, stopwatch.ElapsedMilliseconds, ex.Message);

            throw;
        }
    }
}
