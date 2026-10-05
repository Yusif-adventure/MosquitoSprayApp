using System.Security.Cryptography;
using System.Text;

namespace SmartMosquitoControl.Services;

/// <summary>
/// Device API keys are 256 bits of randomness, so a fast unsalted hash is appropriate
/// (unlike user passwords). Only the hash is stored.
/// </summary>
public static class ApiKeyHasher
{
    public static string Generate() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static string Hash(string apiKey) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey))).ToLowerInvariant();

    public static bool Verify(string apiKey, string expectedHash)
    {
        var actual = Encoding.ASCII.GetBytes(Hash(apiKey));
        var expected = Encoding.ASCII.GetBytes(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
