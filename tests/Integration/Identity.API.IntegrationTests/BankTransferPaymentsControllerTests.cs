using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.RateLimiting;
using SpeedReading.API.Controllers;
using Xunit;

namespace Identity.API.IntegrationTests;

public sealed class BankTransferPaymentsControllerTests
{
    [Fact]
    public void CreateRequest_ShouldUseDedicatedRateLimit()
    {
        var action = typeof(BankTransferPaymentsController).GetMethod(nameof(BankTransferPaymentsController.CreateRequest));

        var rateLimit = action!.GetCustomAttribute<EnableRateLimitingAttribute>();

        rateLimit.Should().NotBeNull();
        rateLimit!.PolicyName.Should().Be("payment-request");
    }
}
