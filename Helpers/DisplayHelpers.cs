using System.Globalization;
using Microsoft.AspNetCore.Html;
using SQPortal.Models.Entities;

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
        var to = partnerEmail ?? string.Empty;
        var subject = Uri.EscapeDataString($"Low-rating feedback alert — {c.Branch} — {c.Date:yyyy-MM-dd}");
        var body = Uri.EscapeDataString(
            $"Case ID: {ShortId(c.Id)}\n" +
            $"Date: {c.Date:yyyy-MM-dd}\n" +
            $"Branch: {c.Branch}\n" +
            $"Customer: {c.CustomerName} ({c.CustomerPhone})\n" +
            $"Branch rating: {(c.BranchRating > 0 ? c.BranchRating.ToString() : "-")}\n" +
            $"Staff rating: {(c.StaffRating > 0 ? c.StaffRating.ToString() : "-")}\n\n" +
            $"Please follow up by {c.DueDate:yyyy-MM-dd}.");
        return $"mailto:{to}?subject={subject}&body={body}";
    }
}
