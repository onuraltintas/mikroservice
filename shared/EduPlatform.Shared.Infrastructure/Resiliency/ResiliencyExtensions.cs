using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace EduPlatform.Shared.Infrastructure.Resiliency;

public static class ResiliencyExtensions
{
    /// <summary>
    /// Adds standard retry and circuit breaker policies to HttpClient.
    /// Default: 3 retries (exponential backoff) and circuit breaker after 5 failures.
    /// </summary>
    public static IHttpClientBuilder AddResiliency(this IHttpClientBuilder builder)
    {
        return builder
            .AddPolicyHandler((serviceProvider, request) => 
                GetRetryPolicy(serviceProvider.GetService<ILogger<HttpClient>>()))
            .AddPolicyHandler((serviceProvider, request) => 
                GetCircuitBreakerPolicy(serviceProvider.GetService<ILogger<HttpClient>>()));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger? logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.NotFound)
            .WaitAndRetryAsync(3, retryAttempt => 
                TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + TimeSpan.FromMilliseconds(new Random().Next(0, 100)),
                (outcome, timespan, retryAttempt, context) =>
                {
                    logger?.LogWarning($"Delaying for {timespan.TotalSeconds} seconds, then making retry {retryAttempt}. Status: {outcome.Result?.StatusCode}, Error: {outcome.Exception?.Message}");
                });
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger? logger)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
                onBreak: (outcome, timespan) =>
                {
                    logger?.LogError($"Circuit broken for {timespan.TotalSeconds} seconds due to: {outcome.Exception?.Message ?? outcome.Result.StatusCode.ToString()}");
                },
                onReset: () =>
                {
                    logger?.LogInformation("Circuit reset. Requests allowed again.");
                });
    }
}
