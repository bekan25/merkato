using Merkato.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Merkato.Api.Services;

public static class AdminPasswordHasher
{
    private static readonly PasswordHasher<User> Hasher = new(
        Options.Create(new PasswordHasherOptions { IterationCount = 210_000 }));

    public static string Hash(User user, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return Hasher.HashPassword(user, password);
    }

    public static bool Verify(User user, string password, string storedHash, out bool rehashNeeded)
    {
        var result = Hasher.VerifyHashedPassword(user, storedHash, password);
        rehashNeeded = result == PasswordVerificationResult.SuccessRehashNeeded;
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
