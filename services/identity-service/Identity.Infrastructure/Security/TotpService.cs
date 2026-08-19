using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;

namespace Identity.Infrastructure.Security;

public static class TotpService
{
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int SecretSizeBytes = 20;
    private const int TimeStepSeconds = 30;

    public static string GenerateSecret()
    {
        return EncodeSecret(RandomNumberGenerator.GetBytes(SecretSizeBytes));
    }

    public static byte[] DecodeSecret(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        var normalized = secret.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();
        var output = new List<byte>((normalized.Length * 5) / 8);
        var bitBuffer = 0;
        var bitCount = 0;

        foreach (var character in normalized)
        {
            var value = Base32Alphabet.IndexOf(character);
            if (value < 0)
            {
                throw new FormatException("The MFA secret is not valid Base32.");
            }

            bitBuffer = (bitBuffer << 5) | value;
            bitCount += 5;

            if (bitCount >= 8)
            {
                output.Add((byte)(bitBuffer >> (bitCount - 8)));
                bitCount -= 8;
                bitBuffer &= (1 << bitCount) - 1;
            }
        }

        return output.ToArray();
    }

    public static string GenerateCode(
        byte[] secret,
        DateTimeOffset timestamp,
        int digits = 6)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length == 0)
        {
            throw new ArgumentException("The MFA secret cannot be empty.", nameof(secret));
        }

        if (digits is < 6 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(digits), "TOTP codes must contain between 6 and 8 digits.");
        }

        return GenerateCodeForStep(secret, timestamp.ToUnixTimeSeconds() / TimeStepSeconds, digits);
    }

    public static long? FindMatchingTimeStep(
        byte[] secret,
        string code,
        DateTimeOffset timestamp,
        int allowedDriftWindows = 1)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (allowedDriftWindows is < 0 or > 2 || code.Length != 6 || code.Any(character => !char.IsAsciiDigit(character)))
        {
            return null;
        }

        var currentStep = timestamp.ToUnixTimeSeconds() / TimeStepSeconds;
        var providedCode = System.Text.Encoding.ASCII.GetBytes(code);

        for (var drift = -allowedDriftWindows; drift <= allowedDriftWindows; drift++)
        {
            var step = currentStep + drift;
            var expectedCode = System.Text.Encoding.ASCII.GetBytes(GenerateCodeForStep(secret, step, 6));
            if (CryptographicOperations.FixedTimeEquals(expectedCode, providedCode))
            {
                return step;
            }
        }

        return null;
    }

    private static string EncodeSecret(ReadOnlySpan<byte> bytes)
    {
        var output = new char[(bytes.Length * 8 + 4) / 5];
        var bitBuffer = 0;
        var bitCount = 0;
        var outputIndex = 0;

        foreach (var value in bytes)
        {
            bitBuffer = (bitBuffer << 8) | value;
            bitCount += 8;

            while (bitCount >= 5)
            {
                output[outputIndex++] = Base32Alphabet[(bitBuffer >> (bitCount - 5)) & 31];
                bitCount -= 5;
                bitBuffer &= (1 << bitCount) - 1;
            }
        }

        if (bitCount > 0)
        {
            output[outputIndex] = Base32Alphabet[(bitBuffer << (5 - bitCount)) & 31];
        }

        return new string(output);
    }

    private static string GenerateCodeForStep(byte[] secret, long step, int digits)
    {
        Span<byte> counter = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(secret, counter, hash);

        var offset = hash[^1] & 0x0F;
        var binaryCode = BinaryPrimitives.ReadInt32BigEndian(hash.Slice(offset, sizeof(int))) & 0x7FFFFFFF;
        var modulus = digits == 8 ? 100_000_000 : digits == 7 ? 10_000_000 : 1_000_000;
        return (binaryCode % modulus).ToString($"D{digits}", CultureInfo.InvariantCulture);
    }
}
