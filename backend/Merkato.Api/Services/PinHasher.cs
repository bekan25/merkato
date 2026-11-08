using System.Security.Cryptography;

namespace Merkato.Api.Services;

public static class PinHasher
{
    private const int CurrentIterations = 600_000;
    private const int LegacyIterations = 100_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    // New hashes carry a version marker so iteration counts can be upgraded safely.
    public static string Hash(string pin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pin);
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            pin, salt, CurrentIterations, HashAlgorithmName.SHA256, HashSize);

        return $"v2.{CurrentIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string pin, string storedHash)
    {
        if (string.IsNullOrEmpty(pin) || string.IsNullOrWhiteSpace(storedHash))
            return false;

        try
        {
            var parts = storedHash.Split('.');
            int iterations;
            string saltPart;
            string hashPart;

            if (parts.Length == 4 && parts[0] == "v2" &&
                int.TryParse(parts[1], out var parsedIterations) &&
                parsedIterations >= CurrentIterations && parsedIterations <= 2_000_000)
            {
                iterations = parsedIterations;
                saltPart = parts[2];
                hashPart = parts[3];
            }
            else if (parts.Length == 2)
            {
                // Backward compatibility for existing 100k-iteration account hashes.
                iterations = LegacyIterations;
                saltPart = parts[0];
                hashPart = parts[1];
            }
            else
            {
                return false;
            }

            byte[] salt = Convert.FromBase64String(saltPart);
            byte[] expectedHash = Convert.FromBase64String(hashPart);
            if (salt.Length != SaltSize || expectedHash.Length != HashSize)
                return false;

            byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
                pin, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
