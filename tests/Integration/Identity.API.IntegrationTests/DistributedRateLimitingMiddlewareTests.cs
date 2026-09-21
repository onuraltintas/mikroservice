using EduPlatform.Gateway.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class DistributedRateLimitingMiddlewareTests
{
    [Fact]
    public async Task NonRateLimitedRoute_ShouldNotResolveRedisConnection()
    {
        var connectionResolved = false;
        var redis = new Lazy<IConnectionMultiplexer>(() =>
        {
            connectionResolved = true;
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is unavailable");
        });
        var nextCalled = false;
        var middleware = new DistributedRateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            redis,
            NullLogger<DistributedRateLimitingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        connectionResolved.Should().BeFalse();
    }

    [Fact]
    public async Task BankTransferRequestList_ShouldNotUsePublicSubmissionRateLimit()
    {
        var connectionResolved = false;
        var redis = new Lazy<IConnectionMultiplexer>(() =>
        {
            connectionResolved = true;
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is unavailable");
        });
        var nextCalled = false;
        var middleware = new DistributedRateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            redis,
            NullLogger<DistributedRateLimitingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/speed-reading/bank-transfer/requests";

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        connectionResolved.Should().BeFalse();
    }

    [Theory]
    [InlineData("/api/auth/login")]
    [InlineData("/api/speed-reading/cms/contact")]
    [InlineData("/api/speed-reading/cms/newsletter/subscribe")]
    [InlineData("/api/speed-reading/bank-transfer/requests")]
    public async Task RateLimitedRoute_ShouldContinueWhenRedisConnectionFails(string path)
    {
        var connectionResolved = false;
        var redis = new Lazy<IConnectionMultiplexer>(() =>
        {
            connectionResolved = true;
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is unavailable");
        });
        var nextCalled = false;
        var middleware = new DistributedRateLimitingMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            redis,
            NullLogger<DistributedRateLimitingMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        await middleware.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        connectionResolved.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }
}
