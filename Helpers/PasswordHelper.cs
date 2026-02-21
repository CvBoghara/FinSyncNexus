using System.Security.Cryptography;

namespace FinSyncNexus.Helpers;

public static class PasswordHelper
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public static string HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        byte[] combined = new byte[SaltSize + HashSize];
        Buffer.BlockCopy(salt, 0, combined, 0, SaltSize);
        Buffer.BlockCopy(hash, 0, combined, SaltSize, HashSize);

        return Convert.ToBase64String(combined);
    }

    public static bool VerifyPassword(string password, string hashedPassword)
    {
        byte[] combined = Convert.FromBase64String(hashedPassword);
        byte[] salt = new byte[SaltSize];
        byte[] storedHash = new byte[HashSize];

        Buffer.BlockCopy(combined, 0, salt, 0, SaltSize);
        Buffer.BlockCopy(combined, SaltSize, storedHash, 0, HashSize);

        byte[] computedHash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
    }
}
