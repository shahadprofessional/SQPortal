using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using SQPortal.Helpers;

namespace SQPortal.Services;

/// <summary>Sends outbound email from the configured mailbox (Mail:FromAddress). Plain text only.</summary>
public class EmailService
{
    private readonly MailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<MailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>The configured sender address, shown in status messages.</summary>
    public string FromAddress => _settings.FromAddress;

    /// <summary>
    /// Sends one email to the given comma-separated recipients. Throws
    /// InvalidOperationException with a user-safe message when mail is not
    /// configured or no valid recipient remains after validation.
    /// </summary>
    public async Task SendAsync(string recipients, string subject, string body)
    {
        if (string.IsNullOrWhiteSpace(_settings.SmtpHost))
        {
            throw new InvalidOperationException(
                "Email not sent — outbound mail is not configured yet. Set Mail:SmtpHost (and credentials) in appsettings.json.");
        }

        // Reject malformed addresses so a stored value cannot smuggle in extra
        // recipients or headers.
        var to = DisplayHelpers.SanitizeMailtoRecipients(recipients);
        if (to.Length == 0)
        {
            throw new InvalidOperationException("Email not sent — no valid recipient address on file.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress, _settings.FromDisplayName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        foreach (var address in to.Split(','))
        {
            message.To.Add(new MailAddress(address));
        }

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.UseStartTls
        };
        if (!string.IsNullOrEmpty(_settings.SmtpUsername))
        {
            client.Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword);
        }

        await client.SendMailAsync(message);
        _logger.LogInformation("Email \"{Subject}\" sent from {From} to {Recipients}", subject, _settings.FromAddress, to);
    }
}
