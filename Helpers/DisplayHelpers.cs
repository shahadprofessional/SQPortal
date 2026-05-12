using System.Globalization;
using Microsoft.AspNetCore.Html;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;

namespace SQPortal.Helpers;

public static class DisplayHelpers
{
    public static HtmlString Stars(int rating)
    {
        if (rating <= 0) return new HtmlString("<span class=\"text-muted\">—</span>");

        var sb = new System.Text.StringBuilder();
        sb.Append("<span class=\"stars-row\" title=\"").Append(rating).Append("/5\">");
        for (var i = 1; i <= 5; i++)
        {
            if (i <= rating) sb.Append('★');
            else sb.Append("<span class=\"star-off\">★</span>");
        }
        sb.Append("</span>");
        return new HtmlString(sb.ToString());
    }

    public static string FormatMonth(string yyyyMm)
    {
        if (string.IsNullOrWhiteSpace(yyyyMm)) return string.Empty;
        if (DateTime.TryParseExact(yyyyMm + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            return d.ToString("MMM yyyy", CultureInfo.InvariantCulture);
        }
        return yyyyMm;
    }

    public static string ShortId(string id) =>
        string.IsNullOrEmpty(id) ? string.Empty :
        id.Length >= 6 ? id[^6..].ToUpperInvariant() : id.ToUpperInvariant();

    public static string BuildLowRatingMailto(FeedbackCase c, string? partnerEmail)
    {
        var partner = LookupData.Partners.FirstOrDefault(p => p.Name == c.BusinessPartner);
        var partnerFullName = partner?.FullName
            ?? (string.IsNullOrEmpty(c.BusinessPartner) ? "Partner" : c.BusinessPartner);
        var to = partnerEmail ?? partner?.Email ?? string.Empty;

        var lines = new List<string>
        {
            $"Date            : {c.Date:yyyy-MM-dd}",
            $"Branch Name     : {c.Branch}"
        };
        if (c.BranchRating > 0)
        {
            lines.Add($"Branch Rating   : {c.BranchRating}/5 ({RatingLabel(c.BranchRating)})");
            if (!string.IsNullOrWhiteSpace(c.BranchComment))
            {
                lines.Add($"Details         : {c.BranchComment}");
            }
        }
        if (!string.IsNullOrWhiteSpace(c.StaffName))
        {
            lines.Add($"Staff Name      : {c.StaffName}");
            if (c.StaffRating > 0)
            {
                lines.Add($"Staff Rating    : {c.StaffRating}/5 ({RatingLabel(c.StaffRating)})");
            }
            if (!string.IsNullOrWhiteSpace(c.StaffComment))
            {
                lines.Add($"Details         : {c.StaffComment}");
            }
        }
        lines.Add($"Customer Name   : {(string.IsNullOrWhiteSpace(c.CustomerName) ? "N/A" : c.CustomerName)}");
        lines.Add($"Customer Number : {(string.IsNullOrWhiteSpace(c.CustomerPhone) ? "N/A" : c.CustomerPhone)}");

        var bodyText = string.Join("\n", new[]
        {
            $"Dear {partnerFullName},",
            string.Empty,
            $"Please find the below low customer feedback notification received on {c.Branch}.",
            string.Empty,
            string.Join("\n", lines),
            string.Empty,
            $"Please contact the customer and update us within 1 working day. Deadline: {c.DueDate:yyyy-MM-dd}",
            string.Empty,
            string.Empty,
            "Customer Experience Management Team"
        });

        var subject = Uri.EscapeDataString($"Low Customer Feedback Notification: {c.Branch}");
        var body = Uri.EscapeDataString(bodyText);
        return $"mailto:{to}?subject={subject}&body={body}";
    }

    private static string RatingLabel(int rating) => rating switch
    {
        1 => "Very Poor",
        2 => "Poor",
        3 => "Fair",
        4 => "Good",
        5 => "Excellent",
        _ => string.Empty
    };
}
