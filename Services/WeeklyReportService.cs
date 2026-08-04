using System.Text;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Weekly;

namespace SQPortal.Services;

public class WeeklyReportService
{
    private static readonly IReadOnlyList<RangeOption> Ranges = new[]
    {
        new RangeOption("thisWeek", "This Week"),
        new RangeOption("lastWeek", "Last Week"),
        new RangeOption("twoWeeks", "Last 2 Weeks"),
        new RangeOption("thisMonth", "This Month")
    };

    private readonly SQPortalDbContext _db;

    public WeeklyReportService(SQPortalDbContext db)
    {
        _db = db;
    }

    public async Task<WeeklyReportViewModel> BuildAsync(string? range, DateOnly today)
    {
        var (start, end, normalizedRange) = ResolveRange(range, today);

        var partnerEmails = await _db.Partners.AsNoTracking().ToDictionaryAsync(p => p.Name, p => p.Email);
        var assignments = await _db.BranchAssignments
            .AsNoTracking()
            .ToDictionaryAsync(a => a.BranchName, a => a.AssignedPartner);

        // A branch only hears about a case once the SQ team has finished the
        // follow-up and judged the complaint genuine. Anything still pending or
        // not marked valid stays out of the report and out of the emails.
        var poorCases = await _db.Cases
            .AsNoTracking()
            .Where(c => c.Date >= start && c.Date <= end
                     && c.FollowUpStatus == FollowUpStatus.Completed
                     && c.CaseValidation == CaseValidation.Valid)
            .ToListAsync();

        var filtered = poorCases
            .Where(c => (c.BranchRating > 0 && c.BranchRating <= 2)
                     || (c.StaffRating > 0 && c.StaffRating <= 2))
            .OrderBy(c => c.Date)
            .ToList();

        var byBranch = filtered
            .GroupBy(c => c.Branch)
            .OrderByDescending(g => g.Count())
            .Select(g =>
            {
                // No hardcoded fallback — a branch is either assigned in Settings or
                // has nobody, and an unassigned branch simply has no recipient.
                var partner = assignments.TryGetValue(g.Key, out var p) ? p : string.Empty;
                var partnerEmail = partnerEmails.TryGetValue(partner, out var e) ? e : string.Empty;
                var cases = g.ToList();
                var staff = cases
                    .Select(c => (c.StaffName ?? string.Empty).Trim())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var item = new BranchReportItemViewModel
                {
                    Branch = g.Key,
                    Partner = partner,
                    PartnerEmail = partnerEmail,
                    Cases = cases,
                    StaffMentioned = staff,
                    BranchFeedbackCount = cases.Count(c => c.BranchRating > 0 && c.BranchRating <= 2),
                    StaffFeedbackCount = cases.Count(c => c.StaffRating > 0 && c.StaffRating <= 2)
                };
                var section = BuildBranchSection(item, start, end, asFullEmail: false);
                var emailBody = BuildBranchSection(item, start, end, asFullEmail: true);
                var subject = $"[SQ] Weekly Service Quality Summary | {item.Branch} | {start:yyyy-MM-dd} to {end:yyyy-MM-dd}";

                item.PreviewText = section;
                item.MailtoLink = BuildMailto(item.PartnerEmail, subject, emailBody);
                return item;
            })
            .ToList();

        var combinedBody = BuildCombinedBody(byBranch, start, end, filtered.Count);
        var combinedSubject = $"[SQ] Weekly Service Quality Summary | {start:yyyy-MM-dd} to {end:yyyy-MM-dd}";

        return new WeeklyReportViewModel
        {
            Range = normalizedRange,
            Start = start,
            End = end,
            Branches = byBranch,
            TotalPoorCases = filtered.Count,
            RangeOptions = Ranges,
            CombinedPreviewText = combinedBody,
            CombinedMailtoLink = filtered.Count > 0
                ? BuildMailto(string.Empty, combinedSubject, combinedBody)
                : string.Empty
        };
    }

    public static IReadOnlyList<RangeOption> RangeOptions => Ranges;

    private static (DateOnly Start, DateOnly End, string Range) ResolveRange(string? range, DateOnly today)
    {
        return range switch
        {
            "lastWeek" => (today.AddDays(-13), today.AddDays(-7), "lastWeek"),
            "twoWeeks" => (today.AddDays(-13), today, "twoWeeks"),
            "thisMonth" => (new DateOnly(today.Year, today.Month, 1), today, "thisMonth"),
            _ => (today.AddDays(-6), today, "thisWeek")
        };
    }

    private static string Stars(int n) =>
        n > 0 ? new string('★', n) + new string('☆', 5 - n) + $" ({n}/5)" : "—";

    private static string BuildBranchSection(BranchReportItemViewModel item, DateOnly start, DateOnly end, bool asFullEmail)
    {
        var sb = new StringBuilder();

        if (asFullEmail)
        {
            sb.AppendLine("Dear Branch Manager,");
            sb.AppendLine();
            sb.AppendLine("Customers receive a feedback message after each branch visit. Customers giving low ratings (1-2 stars) are contacted directly by the SQ team.");
            sb.AppendLine();
            sb.AppendLine($"Period : {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
            sb.AppendLine("Note   : Customer details are not included. The SQ team handles all follow-ups.");
            sb.AppendLine();
        }

        sb.AppendLine("=========================================");
        sb.AppendLine($"  BRANCH: {item.Branch}");
        sb.AppendLine($"  Total Low-Rated Cases : {item.PoorCaseCount}");
        sb.AppendLine($"  Branch Feedbacks      : {item.BranchFeedbackCount}");
        sb.AppendLine($"  Staff Feedbacks       : {item.StaffFeedbackCount}");
        if (item.StaffMentioned.Count > 0)
        {
            sb.AppendLine($"  Staff Mentioned       : {string.Join(", ", item.StaffMentioned)}");
        }
        sb.AppendLine("=========================================");
        sb.AppendLine();

        var branchCases = item.Cases.Where(c => c.BranchRating > 0 && c.BranchRating <= 2).ToList();
        if (branchCases.Count > 0)
        {
            sb.AppendLine("-----------------------------------------");
            sb.AppendLine($"A. BRANCH FEEDBACK ({branchCases.Count})");
            sb.AppendLine("-----------------------------------------");
            for (var i = 0; i < branchCases.Count; i++)
            {
                var c = branchCases[i];
                sb.AppendLine($"{i + 1}. Date: {c.Date:yyyy-MM-dd}  |  Branch Rating: {Stars(c.BranchRating)}");
                if (!string.IsNullOrWhiteSpace(c.BranchComment)) sb.AppendLine($"   Issue      : {c.BranchComment}");
                var rcs = ResolveRootCauses(c);
                if (rcs.Count > 0) sb.AppendLine($"   Root Cause : {string.Join(", ", rcs)}");
                sb.AppendLine();
            }
        }

        var staffCases = item.Cases.Where(c => c.StaffRating > 0 && c.StaffRating <= 2).ToList();
        if (staffCases.Count > 0)
        {
            sb.AppendLine("-----------------------------------------");
            sb.AppendLine($"B. STAFF FEEDBACK ({staffCases.Count})");
            sb.AppendLine("-----------------------------------------");
            for (var i = 0; i < staffCases.Count; i++)
            {
                var c = staffCases[i];
                sb.AppendLine($"{i + 1}. Date: {c.Date:yyyy-MM-dd}  |  Staff Rating: {Stars(c.StaffRating)}");
                if (!string.IsNullOrWhiteSpace(c.StaffName)) sb.AppendLine($"   Staff      : {c.StaffName}");
                if (!string.IsNullOrWhiteSpace(c.StaffComment)) sb.AppendLine($"   Issue      : {c.StaffComment}");
                var rcs = ResolveRootCauses(c);
                if (rcs.Count > 0) sb.AppendLine($"   Root Cause : {string.Join(", ", rcs)}");
                sb.AppendLine();
            }
        }

        var rcCount = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in item.Cases)
        {
            foreach (var r in ResolveRootCauses(c))
            {
                rcCount[r] = rcCount.TryGetValue(r, out var n) ? n + 1 : 1;
            }
        }
        if (rcCount.Count > 0)
        {
            sb.AppendLine("-----------------------------------------");
            sb.AppendLine("MAIN ISSUES THIS WEEK");
            sb.AppendLine("-----------------------------------------");
            foreach (var kvp in rcCount.OrderByDescending(k => k.Value))
            {
                sb.AppendLine($"  - {kvp.Key}: {kvp.Value} case{(kvp.Value > 1 ? "s" : "")}");
            }
            sb.AppendLine();
        }

        if (asFullEmail)
        {
            sb.AppendLine("-----------------------------------------");
            sb.AppendLine("This report is shared for your awareness.");
            sb.AppendLine();
            sb.AppendLine("SQ Team");
        }

        return sb.ToString();
    }

    private static string BuildCombinedBody(IReadOnlyList<BranchReportItemViewModel> branches, DateOnly start, DateOnly end, int totalPoor)
    {
        if (branches.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("Dear Branch Manager,");
        sb.AppendLine();
        sb.AppendLine("Below is a summary of this week's low-rated visits.");
        sb.AppendLine($"Period : {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
        sb.AppendLine($"Total  : {totalPoor} low-rated visit{(totalPoor > 1 ? "s" : "")} across {branches.Count} branch{(branches.Count > 1 ? "es" : "")}");
        sb.AppendLine();

        foreach (var b in branches)
        {
            sb.Append(BuildBranchSection(b, start, end, asFullEmail: false));
        }

        sb.AppendLine();
        sb.AppendLine("SQ Team");

        return sb.ToString();
    }

    private static IReadOnlyList<string> ResolveRootCauses(FeedbackCase c)
    {
        if (c.RootCauses != null && c.RootCauses.Count > 0) return c.RootCauses;
        if (!string.IsNullOrEmpty(c.ValidityStatus)) return new[] { c.ValidityStatus };
        return Array.Empty<string>();
    }

    private static string BuildMailto(string to, string subject, string body)
    {
        var s = Uri.EscapeDataString(subject);
        var b = Uri.EscapeDataString(body);
        return $"mailto:{to}?subject={s}&body={b}";
    }
}
