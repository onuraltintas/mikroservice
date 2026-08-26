using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Payments;

namespace SpeedReading.Application.UnitTests;

public sealed class IyzicoPaymentProviderTests
{
    [Fact]
    public async Task Initializes_checkout_with_signed_request_and_decodes_form_content()
    {
        const string secretKey = "test-secret";
        const string token = "checkout-token";
        var encodedForm = Convert.ToBase64String(Encoding.UTF8.GetBytes("<form>checkout</form>"));
        var responseBody = "{\"status\":\"success\",\"token\":\"checkout-token\",\"signature\":\""
            + ResponseSignature("conversation:checkout-token", secretKey)
            + "\",\"checkoutFormContent\":\""
            + encodedForm
            + "\",\"paymentPageUrl\":\"https://sandbox-cpp.iyzipay.com\"}";
        var handler = new RecordingHandler(
            responseBody);
        var provider = CreateProvider(handler, secretKey);

        var result = await provider.InitializeAsync(new PaymentProviderInitializationRequest(
            "conversation",
            "basket",
            "tr",
            12.50m,
            "TRY",
            "https://example.com/callback",
            "Aylık Plan",
            new PaymentBuyerInfo(
                "buyer",
                "Ada",
                "Lovelace",
                "ada@example.com",
                "+905555555555",
                "12345678901",
                "Test Mah. Test Sok. No: 1",
                "Istanbul",
                "Turkey",
                "34000",
                "127.0.0.1")));

        result.Succeeded.Should().BeTrue();
        result.Token.Should().Be(token);
        result.CheckoutFormContent.Should().Be("<form>checkout</form>");
        handler.LastRequest!.Headers.Authorization!.ToString().Should().StartWith("IYZWSv2 ");
        handler.LastBody.Should().Contain("\"identityNumber\":\"12345678901\"");
        handler.LastBody.Should().Contain("\"itemType\":\"VIRTUAL\"");
    }

    [Fact]
    public async Task Retrieves_and_rejects_a_response_with_an_invalid_signature()
    {
        var handler = new RecordingHandler(
            "{\"status\":\"success\",\"paymentStatus\":\"SUCCESS\",\"fraudStatus\":1,\"paymentId\":\"payment\",\"currency\":\"TRY\",\"basketId\":\"basket\",\"conversationId\":\"conversation\",\"price\":12.5,\"paidPrice\":12.5,\"token\":\"token\",\"signature\":\"invalid\"}");
        var provider = CreateProvider(handler, "test-secret");

        var result = await provider.RetrieveAsync(new PaymentProviderRetrieveRequest(
            "conversation",
            "token",
            "tr"));

        result.RequestSucceeded.Should().BeTrue();
        result.ResponseSignatureValid.Should().BeFalse();
        result.ProviderStatus.Should().Be("SUCCESS");
    }

    private static IyzicoPaymentProvider CreateProvider(RecordingHandler handler, string secretKey) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://sandbox-api.iyzipay.com") },
            new IyzicoOptions
            {
                ApiKey = "test-api-key",
                SecretKey = secretKey,
                CallbackUrl = "https://example.com/callback",
                SuccessRedirectUrl = "https://example.com/success"
            });

    private static string ResponseSignature(string data, string secretKey)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string LastBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
