using System.Net;
using System.Security.Claims;
using Coaching.API.Controllers;
using Coaching.API.Security;
using Coaching.Application.Interfaces;
using Coaching.Application.Subscriptions;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Management;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class CoachingEftRecaptchaTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "invalid-token")]
    [InlineData(true, null)]
    [InlineData(true, "invalid-token")]
    public async Task InvalidCaptchaRejectsStudentAndTeacherRequestsWithoutStoringThem(bool teacher, string? token)
    {
        await using var db = CreateDb();
        var validator = new RecordingValidator(false);
        var controller = CreateController(db, validator, enabled: true, token);
        var request = new CoachingBankTransferRequestCreate(Guid.NewGuid(), "EFT-2026-01", null, null);

        var result = teacher
            ? await controller.CreateTeacherBankTransferRequest(request, "eft-test-idempotency-key")
            : await controller.CreateBankTransferRequest(request, "eft-test-idempotency-key");

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        validator.Token.Should().Be(token);
        validator.Action.Should().Be("coaching_eft_submit");
        (await db.CoachingBankTransferRequests.CountAsync()).Should().Be(0);
        (await db.IdempotencyRecords.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledCaptchaDoesNotSilentlyAllowEftRequests(bool teacher)
    {
        await using var db = CreateDb();
        var validator = new RecordingValidator(true);
        var controller = CreateController(db, validator, enabled: false, "token");
        var request = new CoachingBankTransferRequestCreate(Guid.NewGuid(), "EFT-2026-01", null, null);

        var result = teacher
            ? await controller.CreateTeacherBankTransferRequest(request, "eft-test-idempotency-key")
            : await controller.CreateBankTransferRequest(request, "eft-test-idempotency-key");

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        validator.CallCount.Should().Be(0);
        (await db.CoachingBankTransferRequests.CountAsync()).Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ValidCaptchaAllowsOneStudentOrTeacherPaymentRequest(bool teacher)
    {
        await using var db = CreateDb();
        var service = new CoachingSubscriptionService(db, new IndependentTeacherAuthorization());
        var actor = Guid.NewGuid();
        var plan = await service.CreatePlanAsync(new CoachingSubscriptionPlanRequest(
            "eft-plan", "EFT plan", "Plan", teacher ? "Teacher" : "Individual", 500, false,
            "Monthly", 30, teacher ? 5 : null, [], true, true, 0), actor);
        await service.UpdateSettingsAsync(new CoachingSubscriptionSettingsRequest(
            false, "TRY", "EduIvme", "Bank", "TR330006100519786457841326", null, true),
            actor, "eft-settings-idempotency-key");
        var controller = CreateController(db, new RecordingValidator(true), enabled: true, "valid-token");
        var request = new CoachingBankTransferRequestCreate(plan!.Value, "EFT-2026-01", null, null, true);

        var result = teacher
            ? await controller.CreateTeacherBankTransferRequest(request, "eft-test-idempotency-key")
            : await controller.CreateBankTransferRequest(request, "eft-test-idempotency-key");

        result.Should().BeOfType<OkObjectResult>();
        (await db.CoachingBankTransferRequests.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("coaching_eft_submit", "onuraltintas.net", 0.8, true)]
    [InlineData("newsletter_signup", "onuraltintas.net", 0.8, false)]
    [InlineData("coaching_eft_submit", "attacker.example", 0.8, false)]
    [InlineData("coaching_eft_submit", "onuraltintas.net", 0.2, false)]
    public async Task ProviderResponseMustMatchEftActionHostnameAndScore(string action, string hostname, double score, bool accepted)
    {
        using var handler = new ProviderHandler(action, hostname, score);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.google.com/recaptcha/api/") };
        var validator = new CoachingNewsletterRecaptchaValidator(http, Options(),
            NullLogger<CoachingNewsletterRecaptchaValidator>.Instance);

        (await validator.VerifyAsync("one-time-token", null, "coaching_eft_submit", CancellationToken.None))
            .Should().Be(accepted);
    }

    private static CoachingSubscriptionsController CreateController(
        CoachingDbContext db, RecordingValidator validator, bool enabled, string? token)
    {
        var options = Options();
        options.Enabled = enabled;
        var controller = new CoachingSubscriptionsController(
            new CoachingSubscriptionService(db, new IndependentTeacherAuthorization()), validator, options);
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Name, "Learner"), new Claim(ClaimTypes.Email, "learner@example.test")], "test"));
        if (token is not null) context.Request.Headers["X-Auth-Recaptcha-Token"] = token;
        controller.ControllerContext = new ControllerContext { HttpContext = context };
        return controller;
    }

    private static CoachingNewsletterRecaptchaOptions Options() => new()
    {
        Enabled = true, SiteKey = "public-key", SecretKey = "server-secret", AllowedHostnames = ["onuraltintas.net"]
    };

    private static CoachingDbContext CreateDb() => new(new DbContextOptionsBuilder<CoachingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class RecordingValidator(bool accepted) : ICoachingNewsletterRecaptchaValidator
    {
        public int CallCount { get; private set; }
        public string? Token { get; private set; }
        public string? Action { get; private set; }
        public Task<bool> VerifyAsync(string? token, string? remoteIp, string action, CancellationToken cancellationToken)
        {
            CallCount++;
            Token = token;
            Action = action;
            return Task.FromResult(accepted);
        }
    }

    private sealed class ProviderHandler(string action, string hostname, double score) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { success = true, action, hostname, score }))
            });
    }

    private sealed class IndependentTeacherAuthorization : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds,
            Guid? institutionId, bool admin, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null);
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid userId, IReadOnlyCollection<Guid> studentIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<Guid>>([]);
    }
}
