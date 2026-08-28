using System.Security.Cryptography;
using System.Text;

namespace Api.Authentication;

internal static class CliTokenComparer
{
    public static bool Matches(string presentedToken, string configuredToken)
    {
        var presentedHash = SHA256.HashData(Encoding.UTF8.GetBytes(presentedToken));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredToken));
        return CryptographicOperations.FixedTimeEquals(presentedHash, configuredHash);
    }
}
