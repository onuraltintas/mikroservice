using EduPlatform.Gateway.Middlewares;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class DistributedRateLimitingMiddlewareTests
{
    [Theory]
    [InlineData("/api/auth/forgot-password", "POST", "auth-email", 5, 900)]
    [InlineData("/api/auth/resend-verification-email", "POST", "auth-email", 5, 900)]
    [InlineData("/api/auth/reset-password", "POST", "auth-password-reset", 10, 900)]
    [InlineData("/api/auth/confirm-email", "POST", "auth-email-confirmation", 10, 900)]
    [InlineData("/api/auth/coaching/register/student", "POST", "auth-registration", 15, 60)]
    [InlineData("/api/auth/speed-reading/register/institution", "POST", "auth-registration", 15, 60)]
    [InlineData("/api/auth/login", "POST", "auth", 30, 60)]
    [InlineData("/api/auth/coaching/login", "POST", "auth", 30, 60)]
    public void AuthRoute_UsesSharedEndpointSpecificRateLimit(
        string path,
        string method,
        string expectedName,
        int expectedPermitLimit,
        int expectedWindowSeconds)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;

        var rule = DistributedRateLimitingMiddleware.ResolveRateLimitRule(context.Request);

        rule.Should().NotBeNull();
        rule!.Name.Should().Be(expectedName);
        rule.PermitLimit.Should().Be(expectedPermitLimit);
        rule.Window.Should().Be(TimeSpan.FromSeconds(expectedWindowSeconds));
    }

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
    [InlineData("/api/coaching/cms/newsletter/subscriptions")]
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
