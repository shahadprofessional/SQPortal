using System.Security.Cryptography;
using System.Text;

namespace SQPortal.Helpers;

/// <summary>
/// Checks a sign-in attempt against the credentials configured under "Auth".
/// Supports Auth:PasswordHash ("pbkdf2$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;",
/// preferred) or Auth:Password (plain text, meant for user secrets / environment
/// variables). With neither configured every attempt fails — the portal never
/// runs open.
/// </summary>
public static class CredentialVerifier
{
    public static bool IsConfigured(IConfiguration config) =>
        !string.IsNullOrEmpty(config["Auth:Username"])
        && (!string.IsNullOrEmpty(config["Auth:PasswordHash"])
            || !string.IsNullOrEmpty(config["Auth:Password"]));

    public static bool Verify(IConfiguration config, string? username, string? password)
    {
        if (!IsConfigured(config)) return false;

        var expectedUser = config["Auth:Username"]!;
        var passwordHash = config["Auth:PasswordHash"];
        var plainPassword = config["Auth:Password"];

        // Both halves are always checked, in constant time, so neither the
        // response nor its timing reveals which one was wrong.
        var userOk = FixedTimeEquals(username ?? string.Empty, expectedUser);
        var passOk = !string.IsNullOrEmpty(passwordHash)
            ? VerifyPbkdf2(password ?? string.Empty, passwordHash)
            : FixedTimeEquals(password ?? string.Empty, plainPassword ?? string.Empty);

        return userOk & passOk;
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        // Hashing first gives two equal-length buffers, so the comparison time
        // never depends on how much of the secret matched.
        var ha = SHA256.HashData(Encoding.UTF8.GetBytes(a));
        var hb = SHA256.HashData(Encoding.UTF8.GetBytes(b));
        return CryptographicOperations.FixedTimeEquals(ha, hb);
    }

    private static bool VerifyPbkdf2(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2") return false;
        if (!int.TryParse(parts[1], out var iterations) || iterations < 1) return false;

        byte[] salt;
        byte[] expected;
        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        // A degenerate stored hash must not make every password "match".
        if (salt.Length < 8 || expected.Length < 16) return false;

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
