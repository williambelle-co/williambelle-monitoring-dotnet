using System.Security.Cryptography;
using System.Text;

namespace WilliamBelle.Monitoring;

/// <summary>
/// HMAC-SHA256 signing for sensor payloads. Kept self-contained (duplicated
/// from WilliamBelle.Portal.Core.PayloadVerifier) so the sensor package ships with no
/// William Belle dependencies into customer applications. A round-trip test in
/// WilliamBelle.Portal.Tests pins the two implementations to each other.
/// </summary>
public static class PayloadSigner
{
    /// <summary>Signs a payload with the application's key.</summary>
    /// <param name="payload">The exact JSON that will be transmitted.</param>
    /// <param name="key">The signing key issued for this application.</param>
    /// <returns>A lowercase hexadecimal HMAC-SHA256 signature.</returns>
    public static string Sign(string payload, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        // Convert.ToHexStringLower is .NET 9+; this form is identical output on every target.
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
