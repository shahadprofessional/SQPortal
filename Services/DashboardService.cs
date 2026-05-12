using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Cases;
using SQPortal.Models.ViewModels.Dashboard;

namespace SQPortal.Services;

public class DashboardService
{
    private const string StaffIssueRootCause = "Staff Behavior";
    private const string BranchIssueRootCause = "Long Waiting Time";

    private readonly SQPortalDbContext _db;
    private readonly SlaService _sla;
    private readonly RepeatCustomerService _repeats;

    public DashboardService(SQPortalDbContext db, SlaService sla, RepeatCustomerService repeats)
    {
        _db = db;
        _sla = sla;
        _repeats = repeats;
    }

    public async Task<DashboardViewModel> BuildAsync(DateOnly today)
    {
        var cases = await _db.Cases
            .AsNoTracking()
            .OrderByDescending(c => c.Date)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var repeats = _repeats.Detect(cases, today);
        var flaggedPhones = new HashSet<string>(
            repeats.Where(r => r.MatchType == "Phone")
                   .Select(r => r.Sub.Replace("📞 ", string.Empty).Trim()),
            StringComparer.Ordinal);
        var flaggedNames = new HashSet<string>(
            repeats.Where(r => r.MatchType == "Name")
                   .Select(r => r.Label.Trim().ToLowerInvariant()),
            StringComparer.Ordinal);

        var urgent = cases
            .Where(c => c.FollowUpStatus != FollowUpStatus.Completed && c.DueDate <= today)
            .Select(c => ToRow(c, today))
            .ToList();

        var recent = cases
            .Take(10)
            .Select(c => ToRow(c, today))
            .ToList();

        var dbPartners = await _db.Partners.AsNoTracking().ToListAsync();

        return new DashboardViewModel
        {
            Today = today,
            Total = cases.Count,
            Month = cases.Count(c => c.Date.Year == today.Year && c.Date.Month == today.Month),
            Pending = cases.Count(c => c.FollowUpStatus == FollowUpStatus.Pending),
            Breached = cases.Count(c => _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)),
            Done = cases.Count(c => c.FollowUpStatus == FollowUpStatus.Completed),
            StaffIssues = cases.Count(c => c.RootCauses.Contains(StaffIssueRootCause)),
            BranchIssues = cases.Count(c => c.RootCauses.Contains(BranchIssueRootCause)),
            Valid = cases.Count(c => c.CaseValidation == CaseValidation.Valid),
            NotValid = cases.Count(c => c.CaseValidation == CaseValidation.NotValid),
            UnderReview = cases.Count(c => c.CaseValidation == CaseValidation.UnderReview),
            RepeatCustomers = repeats.ToList(),
            Urgent = urgent,
            Recent = recent,
            PartnerEmails = dbPartners.ToDictionary(p => p.Name, p => p.Email),
            PartnerFullNames = dbPartners.ToDictionary(p => p.Name, p => p.FullName),
            FlaggedPhones = flaggedPhones,
            FlaggedNames = flaggedNames
        };
    }

    private CaseRow ToRow(FeedbackCase c, DateOnly today) => new()
    {
        Case = c,
        AgeDays = today.DayNumber - c.Date.DayNumber,
        SlaBreached = _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)
    };
}
