namespace SQPortal.Services;

/// <summary>
/// Outbound-mail configuration — the "Mail" section of appsettings.json.
/// FromAddress is the mailbox every portal email is sent from.
/// </summary>
public class MailSettings
{
    public const string SectionName = "Mail";

    /// <summary>The address the portal's emails are sent from. Placeholder — change when the real mailbox exists.</summary>
    public string FromAddress { get; set; } = "SQChangeLaterEmail@Example.com";

    public string FromDisplayName { get; set; } = "SQ Service Quality Team";

    /// <summary>SMTP server. Sending fails with a clear message while this is empty.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    /// <summary>STARTTLS on the connection (the usual choice for port 587).</summary>
    public bool UseStartTls { get; set; } = true;

    // Keep real values out of appsettings.json — use environment variables
    // (Mail__SmtpUsername, Mail__SmtpPassword) or "dotnet user-secrets".
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}
