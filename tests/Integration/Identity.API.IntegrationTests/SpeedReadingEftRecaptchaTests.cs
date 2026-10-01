using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.API.Controllers;
using SpeedReading.API.Security;
using SpeedReading.Application.Subscription;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class SpeedReadingEftRecaptchaTests
{
    [Theory]
    [InlineData("speed_reading_eft_submit", "masterhizliokuma.com", 0.9, true)]
    [InlineData("newsletter_signup", "masterhizliokuma.com", 0.9, false)]
    [InlineData("auth_login", "masterhizliokuma.com", 0.9, false)]
    [InlineData("speed_reading_eft_submit", "evil.example", 0.9, false)]
    [InlineData("speed_reading_eft_submit", "masterhizliokuma.com", 0.1, false)]
    public void EftVerification_RequiresMatchingActionHostnameAndScore(string action, string hostname, decimal score, bool expected)
    {
        var options = new GoogleRecaptchaOptions { MinimumScore = 0.5m, AllowedHostnames = ["masterhizliokuma.com"] };
        Assert.Equal(expected, GoogleRecaptchaRules.IsAccepted(options,
            new GoogleRecaptchaVerification(true, score, action, hostname), GoogleRecaptchaRules.PaymentRequestAction));
    }

    [Theory]
    [InlineData(true, false, 403, 0)]
    [InlineData(false, true, 503, 0)]
    [InlineData(true, true, 400, 1)]
    public async Task Submission_RequiresCaptchaBeforeCreatingRequest(bool enabled, bool accepted, int status, int calls)
    {
        var subscriptions = DispatchProxy.Create<ISpeedReadingSubscription, SubscriptionSpy>();
        var validator = new CaptchaSpy(accepted);
        var controller = new BankTransferPaymentsController(subscriptions, validator, new GoogleRecaptchaOptions { Enabled = enabled });
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test"));
        controller.Request.Headers["X-Auth-Recaptcha-Token"] = "test-token";
        var result = Assert.IsAssignableFrom<ObjectResult>(await controller.CreateRequest(
            new CreateBankTransferPaymentRequest(Guid.NewGuid(), "EFT-123", null, null), "request-key"));
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(calls, ((SubscriptionSpy)(object)subscriptions).Calls);
        Assert.Equal(enabled ? 1 : 0, validator.Calls);
        if (enabled) Assert.Equal("speed_reading_eft_submit", validator.Action);
    }

    public class SubscriptionSpy : DispatchProxy
    {
        public int Calls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(ISpeedReadingSubscription.CreateBankTransferPaymentRequestAsync), targetMethod!.Name);
            Calls++;
            return Task.FromResult<BankTransferPaymentRequestSummary?>(null);
        }
    }

    private sealed class CaptchaSpy(bool accepted) : IGoogleRecaptchaValidator
    {
        public int Calls { get; private set; }
        public string? Action { get; private set; }
        public Task<bool> VerifyAsync(string? token, string? remoteIp, string action, CancellationToken cancellationToken)
        {
            Calls++;
            Action = action;
            return Task.FromResult(accepted);
        }
    }
}
