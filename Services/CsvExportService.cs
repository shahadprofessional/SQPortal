using System.Globalization;
using System.Text;
using SQPortal.Models.Entities;

namespace SQPortal.Services;

public class CsvExportService
{
    private static readonly string[] Headers = new[]
    {
        "Id", "Date", "Customer", "Phone", "Ticket", "Branch", "Partner",
        "BranchRating", "BranchComment", "StaffName", "StaffRating", "StaffComment",
        "DueDate", "FollowUpStatus", "FollowUpDate", "FollowUpNotes",
        "CaseValidation", "RootCauses", "ValidationNotes", "EmailSent"
    };

    public byte[] Build(IEnumerable<FeedbackCase> cases)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Headers));

        foreach (var c in cases)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                Escape(c.Id),
                Escape(c.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Escape(c.CustomerName),
                Escape(c.CustomerPhone),
                Escape(c.TicketNumber),
                Escape(c.Branch),
                Escape(c.BusinessPartner),
                Escape(c.BranchRating.ToString()),
                Escape(c.BranchComment),
                Escape(c.StaffName),
                Escape(c.StaffRating.ToString()),
                Escape(c.StaffComment),
                Escape(c.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Escape(c.FollowUpStatus.ToString()),
                Escape(c.FollowUpDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Escape(c.FollowUpNotes),
                Escape(c.CaseValidation.ToString()),
                Escape(string.Join("; ", c.RootCauses)),
                Escape(c.ValidationNotes),
                Escape(c.EmailSent ? "true" : "false")
            }));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuote = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuote ? $"\"{escaped}\"" : escaped;
    }
}
