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
        var branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).Select(b => b.Name).ToListAsync();
        var partners = await _db.Partners.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).ToListAsync();

        var monthPairs = await _db.Cases.AsNoTracking()
            .Select(c => new { c.Date.Year, c.Date.Month })
            .Distinct()
            .ToListAsync();
        var months = monthPairs
            .Select(m => $"{m.Year:D4}-{m.Month:D2}")
            .OrderByDescending(s => s)
            .ToList();

        // Filters are translated to SQL; only the (typically small) filtered
        // slice is loaded for aggregation.
        IQueryable<FeedbackCase> query = _db.Cases.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            var start = DateOnly.FromDateTime(monthDate);
            var end = start.AddMonths(1);
            query = query.Where(c => c.Date >= start && c.Date < end);
        }

        if (!string.IsNullOrWhiteSpace(branch))
        {
            query = query.Where(c => c.Branch == branch);
        }

        if (!string.IsNullOrWhiteSpace(partner))
        {
            query = query.Where(c => c.BusinessPartner == partner);
        }

        var filtered = await query.ToListAsync();

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
            ByPartner = BuildPartnerStats(filtered, partners),
            Month = month,
            Branch = branch,
            Partner = partner,
            Months = months,
            Branches = branches,
            Partners = partners
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

    private static List<PartnerStats> BuildPartnerStats(IEnumerable<FeedbackCase> cases, IEnumerable<string> partnerNames)
    {
        var list = cases.ToList();
        return partnerNames
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
