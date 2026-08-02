using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Cases;
using SQPortal.Models.ViewModels.Dashboard;

namespace SQPortal.Services;

public class DashboardService
{
    private const string OtherRootCausePrefix = "Other: ";

    private readonly SQPortalDbContext _db;
    private readonly SlaService _sla;
    private readonly RepeatCustomerService _repeats;

    public DashboardService(SQPortalDbContext db, SlaService sla, RepeatCustomerService repeats)
    {
        _db = db;
        _sla = sla;
        _repeats = repeats;
    }

    public async Task<DashboardViewModel> BuildAsync(DashboardQuery query, DateOnly today)
    {
        var allCases = await _db.Cases
            .AsNoTracking()
            .OrderByDescending(c => c.Date)
            .ThenByDescending(c => c.Id)
            .ToListAsync();

        var repeats = _repeats.Detect(allCases, today);
        var flaggedPhones = new HashSet<string>(
            repeats.Where(r => r.MatchType == "Phone")
                   .Select(r => r.Sub.Replace("📞 ", string.Empty).Trim()),
            StringComparer.Ordinal);
        var flaggedNames = new HashSet<string>(
            repeats.Where(r => r.MatchType == "Name")
                   .Select(r => r.Label.Trim().ToLowerInvariant()),
            StringComparer.Ordinal);

        // Filters narrow the pool first, so the card counts always describe the list the user is looking at.
        var filtered = ApplyFilters(allCases, query);

        var selectedCard = DashboardCards.Normalize(query.Card);
        var cards = BuildCards(filtered, today, selectedCard);
        var slice = ForCard(filtered, selectedCard, today);

        var totalItems = slice.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)DashboardQuery.PageSize));
        var page = Math.Clamp(query.Page < 1 ? 1 : query.Page, 1, totalPages);

        var items = slice
            .Skip((page - 1) * DashboardQuery.PageSize)
            .Take(DashboardQuery.PageSize)
            .Select(c => ToRow(c, today))
            .ToList();

        var monthOptions = allCases
            .Select(c => $"{c.Date.Year:D4}-{c.Date.Month:D2}")
            .Distinct()
            .OrderByDescending(s => s)
            .ToList();

        var dbPartners = await _db.Partners.AsNoTracking().ToListAsync();

        return new DashboardViewModel
        {
            Today = today,
            Cards = cards,
            SelectedCard = selectedCard,
            SelectedCardLabel = cards.First(c => c.Key == selectedCard).Label,
            Items = items,
            Page = page,
            PageSize = DashboardQuery.PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Search = query.Search,
            Month = query.Month,
            Status = query.Status,
            RootCause = query.RootCause,
            Months = monthOptions,
            RootCauseOptions = LookupData.RootCauses,
            RepeatCustomers = repeats.ToList(),
            PartnerEmails = dbPartners.ToDictionary(p => p.Name, p => p.Email),
            PartnerFullNames = dbPartners.ToDictionary(p => p.Name, p => p.FullName),
            FlaggedPhones = flaggedPhones,
            FlaggedNames = flaggedNames
        };
    }

    private static List<FeedbackCase> ApplyFilters(List<FeedbackCase> cases, DashboardQuery query)
    {
        IEnumerable<FeedbackCase> result = cases;

        if (!string.IsNullOrWhiteSpace(query.Month)
            && DateTime.TryParseExact(query.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            var start = DateOnly.FromDateTime(monthDate);
            var end = start.AddMonths(1);
            result = result.Where(c => c.Date >= start && c.Date < end);
        }

        if (query.Status.HasValue)
        {
            result = result.Where(c => c.FollowUpStatus == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.RootCause))
        {
            string rootCause = query.RootCause;
            result = rootCause == "Other"
                ? result.Where(c => c.RootCauses.Any(r => r.StartsWith(OtherRootCausePrefix, StringComparison.OrdinalIgnoreCase)))
                : result.Where(c => c.RootCauses.Contains(rootCause));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            result = result.Where(c =>
                Contains(c.Id, s) ||
                Contains(c.CustomerName, s) ||
                Contains(c.CustomerPhone, s) ||
                Contains(c.Branch, s) ||
                Contains(c.StaffName, s) ||
                Contains(c.TicketNumber, s) ||
                c.RootCauses.Any(r => Contains(r, s)));
        }

        return result.ToList();
    }

    private static bool Contains(string? value, string term) =>
        !string.IsNullOrEmpty(value) && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private List<DashboardCardViewModel> BuildCards(List<FeedbackCase> cases, DateOnly today, string selectedCard)
    {
        var definitions = new (string Key, string Label, string Hint, string Accent)[]
        {
            (DashboardCards.Total,     "Total",     "All cases matching the filters",       "accent-navy"),
            (DashboardCards.Pending,   "Pending",   "Follow-up not completed yet",          "accent-amber"),
            (DashboardCards.Sla,       "SLA",       "Cases still within their SLA",         "accent-green"),
            (DashboardCards.Breached,  "Breached",  "Follow-up past its due date",          "accent-red"),
            (DashboardCards.Completed, "Completed", "Follow-up completed",                  "accent-green"),
            (DashboardCards.Valid,     "Valid",     "Reviewed and marked valid",            "accent-indigo"),
            (DashboardCards.NotValid,  "Not valid", "Reviewed and marked not valid",        "accent-gray"),
        };

        return definitions.Select(d => new DashboardCardViewModel
        {
            Key = d.Key,
            Label = d.Label,
            Hint = d.Hint,
            Accent = d.Accent,
            Value = ForCard(cases, d.Key, today).Count,
            IsSelected = d.Key == selectedCard
        }).ToList();
    }

    private List<FeedbackCase> ForCard(List<FeedbackCase> cases, string card, DateOnly today) => card switch
    {
        DashboardCards.Pending => cases.Where(c => c.FollowUpStatus == FollowUpStatus.Pending).ToList(),
        DashboardCards.Sla => cases.Where(c => !_sla.IsBreached(c.DueDate, c.FollowUpStatus, today)).ToList(),
        DashboardCards.Breached => cases.Where(c => _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)).ToList(),
        DashboardCards.Completed => cases.Where(c => c.FollowUpStatus == FollowUpStatus.Completed).ToList(),
        DashboardCards.Valid => cases.Where(c => c.CaseValidation == CaseValidation.Valid).ToList(),
        DashboardCards.NotValid => cases.Where(c => c.CaseValidation == CaseValidation.NotValid).ToList(),
        _ => cases.ToList()
    };

    private CaseRow ToRow(FeedbackCase c, DateOnly today) => new()
    {
        Case = c,
        AgeDays = today.DayNumber - c.Date.DayNumber,
        SlaBreached = _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)
    };
}
