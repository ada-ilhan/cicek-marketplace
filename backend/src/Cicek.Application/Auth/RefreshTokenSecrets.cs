using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Cicek.Application.Auth;

/// <summary>Generates opaque refresh tokens and their storage hashes. The raw token is never persisted.</summary>
public static class RefreshTokenSecrets
{
    private const int TokenBytes = 32;

    public static (string Token, string Hash) Generate()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenBytes));
        return (token, Hash(token));
    }

    /// <summary>
    /// SHA-256 is appropriate here (unlike for passwords) because the input is 256 bits of randomness,
    /// so brute force against a leaked hash is infeasible.
    /// </summary>
    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
