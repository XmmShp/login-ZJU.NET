using System.Globalization;
using System.Numerics;

namespace LoginZju;

/// <summary>
/// Provides RSA encryption compatible with the ZJUAM public key endpoint.
/// </summary>
internal static class RsaHelper
{
    /// <summary>
    /// Encrypts a password using the RSA public key parameters returned by ZJUAM.
    /// </summary>
    /// <param name="password">The plaintext password.</param>
    /// <param name="exponentHex">The RSA exponent as a hex string.</param>
    /// <param name="modulusHex">The RSA modulus as a hex string.</param>
    /// <returns>The encrypted password as a zero-padded hex string.</returns>
    public static string Encrypt(string password, string exponentHex, string modulusHex)
    {
        var pwd = BigInteger.Zero;
        foreach (var c in password)
        {
            pwd = pwd * 256 + c;
        }

        // Prepend "0" to ensure BigInteger.Parse treats hex as positive.
        var n = BigInteger.Parse("0" + modulusHex, NumberStyles.HexNumber);
        var e = BigInteger.Parse("0" + exponentHex, NumberStyles.HexNumber);

        var encrypted = BigInteger.ModPow(pwd, e, n);
        return encrypted.ToString("x").PadLeft(modulusHex.Length, '0');
    }
}
