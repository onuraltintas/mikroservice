using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Infrastructure.Payments;
using SpeedReading.Application.Content;
using SpeedReading.Application.Subscription;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/payment")]
public sealed class PaymentsController(
    ISpeedReadingSubscription subscriptions,
    IyzicoOptions iyzicoOptions) : ControllerBase
{
    [HttpPost("initialize")]
    [Authorize]
    public async Task<IActionResult> Initialize(
        [FromBody] InitializePaymentRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (request is null)
        {
            return BadRequest(new { success = false, message = "A payment plan is required." });
        }

        var enrichedRequest = request with
        {
            PhoneNumber = request.PhoneNumber ?? User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("phone_number"),
            IdentityNumber = request.IdentityNumber ?? User.FindFirstValue("identityNumber") ?? User.FindFirstValue("nationalId")
        };
        var result = await subscriptions.InitializePaymentAsync(
            userId,
            enrichedRequest,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        if (!result.Available)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message });
        }

        return result.Succeeded
            ? Ok(new { token = result.Token, paymentPageUrl = result.PaymentPageUrl, checkoutFormContent = result.CheckoutFormContent })
            : BadRequest(new { success = false, message = result.Message });
    }

    [HttpPost("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        [FromForm] string? token,
        CancellationToken cancellationToken = default)
    {
        token ??= Request.Query["token"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { success = false, message = "Payment token is required." });
        }

        var result = await subscriptions.ProcessPaymentCallbackAsync(token, cancellationToken);
        if (!result.Available)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message });
        }

        var redirectUrl = QueryHelpers.AddQueryString(
            iyzicoOptions.SuccessRedirectUrl!,
            new Dictionary<string, string?>
            {
                ["success"] = result.Success ? "true" : "false",
                ["token"] = token
            });
        return Redirect(redirectUrl);
    }

    [HttpGet("verify")]
    [Authorize]
    public async Task<IActionResult> Verify(
        [FromQuery] string token,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return BadRequest(new { success = false, message = "Payment token is required." });
        }

        var result = await subscriptions.VerifyPaymentAsync(userId, token, cancellationToken);
        if (!result.Available)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = result.Success,
            status = result.Status,
            planName = result.PlanName,
            amount = result.Amount,
            subscriptionId = result.SubscriptionId,
            message = result.Message
        });
    }

    [HttpGet]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await subscriptions.GetPaymentsAsync(page, pageSize, status, search, cancellationToken);
        return Ok(new
        {
            total = result.TotalCount,
            result.PageNumber,
            result.PageSize,
            items = result.Items
        });
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
