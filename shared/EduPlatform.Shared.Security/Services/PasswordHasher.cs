using System.Security.Cryptography;
using System.Text;
using EduPlatform.Shared.Security.Interfaces;

namespace EduPlatform.Shared.Security.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int Iterations = 600_000;

    public void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
    {
        ArgumentNullException.ThrowIfNull(password);

        passwordSalt = RandomNumberGenerator.GetBytes(SaltSize);
        passwordHash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            passwordSalt,
            Iterations,
            HashAlgorithmName.SHA512,
            HashSize);
    }

    public bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentNullException.ThrowIfNull(storedHash);
        ArgumentNullException.ThrowIfNull(storedSalt);

        if (storedHash.Length == HashSize && storedSalt.Length == SaltSize)
        {
            var computedHash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                storedSalt,
                Iterations,
                HashAlgorithmName.SHA512,
                HashSize);

            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }

        // Compatibility path for existing accounts. Successful legacy logins
        // are rehashed by the login handler using the current parameters.
        if (storedHash.Length == 64 && storedSalt.Length == 128)
        {
            using var legacyHmac = new HMACSHA512(storedSalt);
            var computedHash = legacyHmac.ComputeHash(Encoding.UTF8.GetBytes(password));
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }

        return false;
    }

    public bool NeedsRehash(byte[] storedHash, byte[] storedSalt)
    {
        ArgumentNullException.ThrowIfNull(storedHash);
        ArgumentNullException.ThrowIfNull(storedSalt);

        return storedHash.Length != HashSize || storedSalt.Length != SaltSize;
    }
}
