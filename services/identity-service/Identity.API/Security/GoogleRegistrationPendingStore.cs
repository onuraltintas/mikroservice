using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Identity.Domain.Enums;
using StackExchange.Redis;

namespace Identity.API.Security;

public sealed class GoogleRegistrationPendingStore(
    IConnectionMultiplexer redis,
    IDataProtectionProvider protection)
{
    public string Key(string token, PlatformProduct product) =>
        $"EduPlatform:google-registration:{product}:{token}";

    public async Task<GoogleRegistrationPending> CreateAsync(string idToken, PlatformProduct product)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var protector = protection.CreateProtector("GoogleRegistrationPending", product.ToString());
        await redis.GetDatabase().StringSetAsync(Key(token, product), protector.Protect(idToken), TimeSpan.FromMinutes(5));
        return new GoogleRegistrationPending(true, token, DateTimeOffset.UtcNow.AddMinutes(5));
    }

    public async Task<string?> ConsumeAsync(string token, PlatformProduct product)
    {
        if (token.Length != 64 || !token.All(Uri.IsHexDigit)) return null;
        var value = await redis.GetDatabase().StringGetDeleteAsync(Key(token, product));
        if (value.IsNullOrEmpty) return null;
        try
        {
            return protection.CreateProtector("GoogleRegistrationPending", product.ToString()).Unprotect(value.ToString());
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}

public sealed record GoogleRegistrationPending(bool RequiresLegalAcceptance, string RegistrationToken, DateTimeOffset RegistrationExpiresAt);
