using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Analytics;

namespace SQPortal.Services;

public class AnalyticsService
{
    private const string OtherRootCausePrefix = "Other: ";

    private readonly SQPortalDbContext _db;
    private readonly SlaService _sla;

    public AnalyticsService(SQPortalDbContext db, SlaService sla)
    {
        _db = db;
        _sla = sla;
    }

    public async Task<AnalyticsViewModel> BuildAsync(string? month, string? branch, string? partner, DateOnly today)
    {
        var allCases = await _db.Cases.AsNoTracking().ToListAsync();

        var months = allCases
            .Select(c => $"{c.Date.Year:D4}-{c.Date.Month:D2}")
            .Distinct()
            .OrderByDescending(s => s)
            .ToList();

        var filtered = allCases.Where(c =>
        {
            if (!string.IsNullOrWhiteSpace(month))
            {
                var caseMonth = $"{c.Date.Year:D4}-{c.Date.Month:D2}";
                if (caseMonth != month) return false;
            }
            if (!string.IsNullOrWhiteSpace(branch) && c.Branch != branch) return false;
            if (!string.IsNullOrWhiteSpace(partner) && c.BusinessPartner != partner) return false;
            return true;
        }).ToList();

        var total = filtered.Count;
        var done = filtered.Count(c => c.FollowUpStatus == FollowUpStatus.Completed);
        var open = filtered.Count(c => c.FollowUpStatus == FollowUpStatus.Pending);
        var breached = filtered.Count(c => _sla.IsBreached(c.DueDate, c.FollowUpStatus, today));
        var slaOk = filtered.Count(c => c.FollowUpStatus == FollowUpStatus.Completed
                                        && (!c.FollowUpDate.HasValue || c.FollowUpDate.Value <= c.DueDate));
        var rated = filtered.Where(c => c.BranchRating > 0).ToList();

        return new AnalyticsViewModel
        {
            Total = total,
            Done = done,
            Open = open,
            Breached = breached,
            ResolutionRate = total > 0 ? (int)Math.Round(done / (double)total * 100) : 0,
            SlaCompliance = done > 0 ? (int)Math.Round(slaOk / (double)done * 100) : 0,
            AvgBranchRating = rated.Count > 0
                ? Math.Round(rated.Average(c => (double)c.BranchRating), 1)
                : 0,
            ByBranch = BuildBranchStats(filtered),
            RootCauseDistribution = BuildRootCauseStats(filtered),
            ByPartner = BuildPartnerStats(filtered),
            Month = month,
            Branch = branch,
            Partner = partner,
            Months = months,
            Branches = LookupData.Branches,
            Partners = LookupData.PartnerNames
        };
    }

    private static List<BranchStats> BuildBranchStats(IEnumerable<FeedbackCase> cases)
    {
        return cases
            .GroupBy(c => c.Branch)
            .Select(g =>
            {
                var totalG = g.Count();
                var doneG = g.Count(c => c.FollowUpStatus == FollowUpStatus.Completed);
                var rated = g.Where(c => c.BranchRating > 0).ToList();
                return new BranchStats
                {
                    Name = g.Key,
                    Total = totalG,
                    Done = doneG,
                    ResolutionRate = totalG > 0 ? (int)Math.Round(doneG / (double)totalG * 100) : 0,
                    AvgBranchRating = rated.Count > 0
                        ? Math.Round(rated.Average(c => (double)c.BranchRating), 1)
                        : 0
                };
            })
            .OrderByDescending(s => s.Total)
            .ToList();
    }

    private static List<RootCauseStats> BuildRootCauseStats(IEnumerable<FeedbackCase> cases)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var c in cases)
        {
            var sources = (c.RootCauses != null && c.RootCauses.Count > 0)
                ? c.RootCauses
                : (string.IsNullOrEmpty(c.ValidityStatus) ? new List<string>() : new List<string> { c.ValidityStatus });

            foreach (var r in sources)
            {
                var key = r.StartsWith(OtherRootCausePrefix, StringComparison.Ordinal) ? "Other" : r;
                counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
            }
        }

        var total = counts.Values.Sum();
        return counts
            .OrderByDescending(kvp => kvp.Value)
            .Select(kvp => new RootCauseStats
            {
                Name = kvp.Key,
                Count = kvp.Value,
                Percent = total > 0 ? (int)Math.Round(kvp.Value / (double)total * 100) : 0
            })
            .ToList();
    }

    private static List<PartnerStats> BuildPartnerStats(IEnumerable<FeedbackCase> cases)
    {
        var list = cases.ToList();
        return LookupData.PartnerNames
            .Select(name =>
            {
                var pc = list.Where(c => c.BusinessPartner == name).ToList();
                var dn = pc.Count(c => c.FollowUpStatus == FollowUpStatus.Completed);
                var pend = pc.Count(c => c.FollowUpStatus == FollowUpStatus.Pending);
                var slaOk = pc.Count(c => c.FollowUpStatus == FollowUpStatus.Completed
                                          && (!c.FollowUpDate.HasValue || c.FollowUpDate.Value <= c.DueDate));
                return new PartnerStats
                {
                    Partner = name,
                    Total = pc.Count,
                    Done = dn,
                    Pending = pend,
                    SlaRate = dn > 0 ? (int)Math.Round(slaOk / (double)dn * 100) : 0
                };
            })
            .ToList();
    }
}
