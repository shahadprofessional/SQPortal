namespace SQPortal.Helpers;

/// <summary>
/// Per-request Content-Security-Policy script nonce, set by the
/// security-header middleware in Program.cs and required on inline scripts.
/// </summary>
public static class Csp
{
    public const string NonceKey = "csp-nonce";

    public static string Nonce(HttpContext context) =>
        context.Items[NonceKey] as string ?? string.Empty;
}
