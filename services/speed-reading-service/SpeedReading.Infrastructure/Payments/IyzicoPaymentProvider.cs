using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SpeedReading.Application.Subscription;

namespace SpeedReading.Infrastructure.Payments;

public sealed class IyzicoPaymentProvider(HttpClient httpClient, IyzicoOptions options)
    : ISpeedReadingPaymentProvider
{
    private const string InitializePath = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
    private const string RetrievePath = "/payment/iyzipos/checkoutform/auth/ecom/detail";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => options.IsConfigured;

    public async Task<PaymentProviderInitializationResult> InitializeAsync(
        PaymentProviderInitializationRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new CheckoutInitializePayload(
            request.Locale,
            request.ConversationId,
            request.Price,
            request.Price,
            request.Currency,
            request.BasketId,
            "PRODUCT",
            request.CallbackUrl,
            [1, 2, 3, 6, 9, 12],
            new BuyerPayload(
                request.Buyer.Id,
                request.Buyer.Name,
                request.Buyer.Surname,
                request.Buyer.IdentityNumber,
                request.Buyer.Email,
                request.Buyer.PhoneNumber,
                request.Buyer.BillingAddress,
                request.Buyer.City,
                request.Buyer.Country,
                request.Buyer.IpAddress,
                request.Buyer.ZipCode),
            null,
            new AddressPayload(
                request.Buyer.BillingAddress,
                $"{request.Buyer.Name} {request.Buyer.Surname}".Trim(),
                request.Buyer.City,
                request.Buyer.Country,
                request.Buyer.ZipCode),
            [new BasketItemPayload(
                request.BasketId,
                request.Price,
                request.ItemName,
                "Hızlı Okuma",
                "VIRTUAL")]);
        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var response = await SendAsync(InitializePath, body, cancellationToken);

        if (!response.RequestSucceeded)
        {
            return new(false, null, null, null, response.ErrorMessage, response.RawResponse);
        }

        using var document = response.Document!;
        var token = GetString(document.RootElement, "token");
        var signature = GetString(document.RootElement, "signature");
        var signatureValid = !options.RequireResponseSignature
            || IyzicoResponseSignatureValidator.ValidateInitialization(
                request.ConversationId,
                token,
                signature,
                options.SecretKey!);
        if (!signatureValid)
        {
            return new(false, null, null, null, "Payment provider response signature is invalid.", response.RawResponse);
        }

        if (!string.Equals(GetString(document.RootElement, "status"), "success", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(token))
        {
            return new(false, null, null, null, GetErrorMessage(document.RootElement), response.RawResponse);
        }

        var checkoutFormContent = DecodeCheckoutFormContent(
            GetString(document.RootElement, "checkoutFormContent"));
        return new(
            true,
            token,
            GetString(document.RootElement, "paymentPageUrl"),
            checkoutFormContent,
            null,
            response.RawResponse);
    }

    public async Task<PaymentProviderRetrieveResult> RetrieveAsync(
        PaymentProviderRetrieveRequest request,
        CancellationToken cancellationToken = default)
    {
        var payload = new CheckoutRetrievePayload(request.Locale, request.ConversationId, request.Token);
        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var response = await SendAsync(RetrievePath, body, cancellationToken);
        if (!response.RequestSucceeded)
        {
            return new(false, false, null, null, null, null, null, null, null, null, null, response.ErrorMessage, response.RawResponse);
        }

        using var document = response.Document!;
        var root = document.RootElement;
        var result = new PaymentProviderRetrieveResult(
            true,
            false,
            GetString(root, "paymentStatus"),
            GetInt(root, "fraudStatus"),
            GetString(root, "paymentId"),
            GetString(root, "currency"),
            GetString(root, "basketId"),
            GetString(root, "conversationId"),
            GetDecimal(root, "price"),
            GetDecimal(root, "paidPrice"),
            GetString(root, "token"),
            GetErrorMessage(root),
            response.RawResponse);
        var signatureValid = !options.RequireResponseSignature
            || IyzicoResponseSignatureValidator.ValidateRetrieve(
                result.ProviderStatus,
                result.PaymentId,
                result.Currency,
                result.BasketId,
                result.ConversationId,
                result.PaidPrice,
                result.Price,
                result.Token,
                GetString(root, "signature"),
                options.SecretKey!);
        return result with { ResponseSignatureValid = signatureValid };
    }

    private async Task<ProviderResponse> SendAsync(
        string path,
        string body,
        CancellationToken cancellationToken)
    {
        var randomKey = CreateRandomKey();
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path);
        httpRequest.Content = new StringContent(body, Encoding.UTF8, "application/json");
        httpRequest.Headers.Authorization = AuthenticationHeaderValue.Parse(
            IyzicoRequestSigner.CreateAuthorization(
                options.ApiKey!,
                options.SecretKey!,
                path,
                body,
                randomKey));
        httpRequest.Headers.TryAddWithoutValidation("x-iyzi-rnd", randomKey);

        try
        {
            using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);
            var rawResponse = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            if (!httpResponse.IsSuccessStatusCode)
            {
                return new(false, rawResponse, null, rawResponse);
            }

            var document = JsonDocument.Parse(rawResponse);
            return new(true, rawResponse, document, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, string.Empty, null, "Payment provider request timed out.");
        }
        catch (HttpRequestException)
        {
            return new(false, string.Empty, null, "Payment provider could not be reached.");
        }
        catch (JsonException)
        {
            return new(false, string.Empty, null, "Payment provider returned an invalid response.");
        }
    }

    private static string CreateRandomKey() =>
        $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}{Random.Shared.Next(100000, 999999)}";

    private static string? GetString(JsonElement root, string propertyName) =>
        !root.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null
            ? null
            : value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.GetRawText();

    private static int? GetInt(JsonElement root, string propertyName) =>
        !root.TryGetProperty(propertyName, out var value)
            ? null
            : value.TryGetInt32(out var number)
                ? number
                : int.TryParse(GetString(root, propertyName), out var parsed) ? parsed : null;

    private static decimal? GetDecimal(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.TryGetDecimal(out var number))
        {
            return number;
        }

        return decimal.TryParse(
            GetString(root, propertyName),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var parsed) ? parsed : null;
    }

    private static string? GetErrorMessage(JsonElement root)
    {
        var errorMessage = GetString(root, "errorMessage");
        var errorCode = GetString(root, "errorCode");
        return string.IsNullOrWhiteSpace(errorCode)
            ? errorMessage
            : $"{errorMessage ?? "Payment provider rejected the request."} ({errorCode})";
    }

    private static string? DecodeCheckoutFormContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.TrimStart().StartsWith('<'))
        {
            return content;
        }

        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(content));
            return decoded.Contains('<') ? decoded : content;
        }
        catch (FormatException)
        {
            return content;
        }
    }

    private sealed record ProviderResponse(bool RequestSucceeded, string RawResponse, JsonDocument? Document, string? ErrorMessage);

    private sealed record CheckoutInitializePayload(
        string Locale,
        string ConversationId,
        decimal Price,
        decimal PaidPrice,
        string Currency,
        string BasketId,
        string PaymentGroup,
        string CallbackUrl,
        int[] EnabledInstallments,
        BuyerPayload Buyer,
        AddressPayload? ShippingAddress,
        AddressPayload BillingAddress,
        BasketItemPayload[] BasketItems);

    private sealed record CheckoutRetrievePayload(string Locale, string ConversationId, string Token);

    private sealed record BuyerPayload(
        string Id,
        string Name,
        string Surname,
        string IdentityNumber,
        string Email,
        string GsmNumber,
        string RegistrationAddress,
        string City,
        string Country,
        string Ip,
        string ZipCode);

    private sealed record AddressPayload(
        string Address,
        string ContactName,
        string City,
        string Country,
        string ZipCode);

    private sealed record BasketItemPayload(
        string Id,
        decimal Price,
        string Name,
        string Category1,
        string ItemType);
}
