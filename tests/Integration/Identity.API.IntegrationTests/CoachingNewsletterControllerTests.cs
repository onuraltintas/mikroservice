using Coaching.API.Controllers;
using Coaching.API.Security;
using Coaching.Application.Newsletters;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingNewsletterControllerTests
{
    [Fact]
    public void PublicPostActionsUseTheLocalRateLimitFallback()
    {
        var methods = new[]
        {
            nameof(CoachingNewsletterController.Subscribe),
            nameof(CoachingNewsletterController.Confirm),
            nameof(CoachingNewsletterController.Unsubscribe)
        };

        foreach (var methodName in methods)
        {
            var attribute = typeof(CoachingNewsletterController).GetMethod(methodName)!
                .GetCustomAttributes(typeof(EnableRateLimitingAttribute), inherit: true)
                .Cast<EnableRateLimitingAttribute>()
                .Single();
            attribute.PolicyName.Should().Be("public-newsletter-write");
        }
    }

    [Fact]
    public async Task SubscribeRejectsRequestsWhenCaptchaVerificationFails()
    {
        await using var db = CreateDbContext();
        var validator = new RecordingRecaptchaValidator(false);
        var controller = CreateController(db, validator);

        var result = await controller.Subscribe(new CoachingNewsletterController.SubscribeRequest(
            "learner@example.com",
            true,
            RecaptchaToken: "invalid-token"));

        result.Should().BeOfType<BadRequestObjectResult>();
        validator.Action.Should().Be(CoachingNewsletterRecaptchaRules.NewsletterSignupAction);
        validator.RemoteIp.Should().Be("192.0.2.7");
        (await db.CoachingNewsletterSubscribers.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SubscribeSkipsCaptchaForFilledHoneypotAndDoesNotStoreTheSubmission()
    {
        await using var db = CreateDbContext();
        var validator = new RecordingRecaptchaValidator(false);
        var controller = CreateController(db, validator);

        var result = await controller.Subscribe(new CoachingNewsletterController.SubscribeRequest(
            "bot@example.com",
            true,
            Honeypot: "filled-by-bot"));

        result.Should().BeOfType<AcceptedResult>();
        validator.CallCount.Should().Be(0);
        (await db.CoachingNewsletterSubscribers.CountAsync()).Should().Be(0);
    }

    private static CoachingNewsletterController CreateController(
        CoachingDbContext db,
        RecordingRecaptchaValidator validator)
    {
        var emailDelivery = new RecordingEmailDelivery();
        var newsletter = new CoachingNewsletterService(db, emailDelivery, new FixedPrivacyVersionProvider(), "https://onuraltintas.net");
        var controller = new CoachingNewsletterController(
            newsletter,
            validator,
            new CoachingNewsletterRecaptchaOptions());
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-EduPlatform-Client-IP"] = "192.0.2.7";
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    private static CoachingDbContext CreateDbContext() => new(
        new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class RecordingRecaptchaValidator(bool result) : ICoachingNewsletterRecaptchaValidator
    {
        public int CallCount { get; private set; }
        public string? RemoteIp { get; private set; }
        public string? Action { get; private set; }

        public Task<bool> VerifyAsync(string? token, string? remoteIp, string action, CancellationToken cancellationToken)
        {
            CallCount++;
            RemoteIp = remoteIp;
            Action = action;
            return Task.FromResult(result);
        }
    }

    private sealed class RecordingEmailDelivery : ICoachingNewsletterEmailDelivery
    {
        public Task QueueConfirmationAsync(Guid messageId, string recipient, string body, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FixedPrivacyVersionProvider : ICoachingSharedLegalPageVersionProvider
    {
        public Task<int?> GetPublishedVersionAsync(string slug, CancellationToken cancellationToken = default) => Task.FromResult<int?>(1);
    }
}
