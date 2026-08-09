namespace SQPortal.Helpers;

/// <summary>
/// Access to the per-request Content-Security-Policy script nonce set by the
/// security-header middleware in Program.cs. Inline page scripts carry this
/// nonce; everything without it is blocked by the browser.
/// </summary>
public static class Csp
{
    public const string NonceKey = "csp-nonce";

    public static string Nonce(HttpContext context) =>
        context.Items[NonceKey] as string ?? string.Empty;
}
