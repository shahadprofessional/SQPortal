namespace SQPortal.Services;

/// <summary>Outbound-mail configuration, bound from the "Mail" section of appsettings.json.</summary>
public class MailSettings
{
    public const string SectionName = "Mail";

    /// <summary>The address the portal's emails are sent from. Placeholder until the real mailbox exists.</summary>
    public string FromAddress { get; set; } = "SQChangeLaterEmail@Example.com";

    public string FromDisplayName { get; set; } = "SQ Service Quality Team";

    /// <summary>SMTP server. Sending fails with a clear message while this is empty.</summary>
    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    /// <summary>STARTTLS on the connection (the usual choice for port 587).</summary>
    public bool UseStartTls { get; set; } = true;

    // Real credentials belong in environment variables or user secrets, not in
    // appsettings.json.
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
}
