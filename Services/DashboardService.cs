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

    public DashboardService(SQPortalDbContext db, SlaService sla)
    {
        _db = db;
        _sla = sla;
    }

    public async Task<DashboardViewModel> BuildAsync(DashboardQuery query, DateOnly today)
    {
        var selectedCard = DashboardCards.Normalize(query.Card);

        List<DashboardCardViewModel> cards;
        List<FeedbackCase> pageItems;
        int totalItems;

        if (NeedsInMemoryFiltering(query))
        {
            // Search and root-cause filters inspect fields SQL cannot match
            // (JSON root causes, case-number text); narrow by month/status in
            // SQL, finish in memory.
            var pool = ApplyResidualFilters(await SqlFiltered(query).ToListAsync(), query);
            cards = BuildCardsInMemory(pool, today, selectedCard);
            var ordered = SortInMemory(ForCardInMemory(pool, selectedCard, today), today);
            totalItems = ordered.Count;
            pageItems = ordered
                .Skip((NormalizePage(query.Page, totalItems, out var pageA) - 1) * DashboardQuery.PageSize)
                .Take(DashboardQuery.PageSize)
                .ToList();
            query.Page = pageA;
        }
        else
        {
            // Common path: counts, ordering and paging all happen in SQL.
            cards = await BuildCardsSqlAsync(query, today, selectedCard);
            totalItems = cards.First(c => c.Key == selectedCard).Value;
            var page = NormalizePage(query.Page, totalItems, out var pageB);
            pageItems = await OrderForList(ForCardSql(SqlFiltered(query), selectedCard, today), today)
                .Skip((page - 1) * DashboardQuery.PageSize)
                .Take(DashboardQuery.PageSize)
                .ToListAsync();
            query.Page = pageB;
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)DashboardQuery.PageSize));

        var monthOptions = await MonthOptionsAsync();
        var dbPartners = await _db.Partners.AsNoTracking().ToListAsync();

        return new DashboardViewModel
        {
            Today = today,
            Cards = cards,
            SelectedCard = selectedCard,
            SelectedCardLabel = cards.First(c => c.Key == selectedCard).Label,
            Items = pageItems.Select(c => ToRow(c, today)).ToList(),
            Page = query.Page,
            PageSize = DashboardQuery.PageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            Search = query.Search,
            Month = query.Month,
            Status = query.Status,
            RootCause = query.RootCause,
            Months = monthOptions,
            RootCauseOptions = LookupData.RootCauses,
            PartnerEmails = dbPartners.ToDictionary(p => p.Name, p => p.Email),
            PartnerFullNames = dbPartners.ToDictionary(p => p.Name, p => p.FullName),
        };
    }

    /// <summary>Every case behind the current card and filters, in list order, unpaged.</summary>
    public async Task<List<FeedbackCase>> GetCasesAsync(DashboardQuery query, DateOnly today)
    {
        var card = DashboardCards.Normalize(query.Card);

        if (NeedsInMemoryFiltering(query))
        {
            var pool = ApplyResidualFilters(await SqlFiltered(query).ToListAsync(), query);
            return SortInMemory(ForCardInMemory(pool, card, today), today);
        }

        return await OrderForList(ForCardSql(SqlFiltered(query), card, today), today).ToListAsync();
    }

    // ---------- SQL path ----------

    private static bool NeedsInMemoryFiltering(DashboardQuery query) =>
        !string.IsNullOrWhiteSpace(query.Search) || !string.IsNullOrWhiteSpace(query.RootCause);

    /// <summary>Month and status filters, applied as a translated query.</summary>
    private IQueryable<FeedbackCase> SqlFiltered(DashboardQuery query)
    {
        IQueryable<FeedbackCase> q = _db.Cases.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Month)
            && DateTime.TryParseExact(query.Month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            var start = DateOnly.FromDateTime(monthDate);
            var end = start.AddMonths(1);
            q = q.Where(c => c.Date >= start && c.Date < end);
        }

        if (query.Status.HasValue)
        {
            var status = query.Status.Value;
            q = q.Where(c => c.FollowUpStatus == status);
        }

        return q;
    }

    private IQueryable<FeedbackCase> ForCardSql(IQueryable<FeedbackCase> q, string card, DateOnly today)
    {
        // Mirrors SlaService.IsBreached: nothing counts as breached on a
        // non-working day.
        var slaActive = !_sla.IsWeekend(today);

        return card switch
        {
            DashboardCards.Pending => q.Where(c => c.FollowUpStatus == FollowUpStatus.Pending),
            DashboardCards.Completed => q.Where(c => c.FollowUpStatus == FollowUpStatus.Completed),
            DashboardCards.Valid => q.Where(c => c.CaseValidation == CaseValidation.Valid),
            DashboardCards.NotValid => q.Where(c => c.CaseValidation == CaseValidation.NotValid),
            DashboardCards.Breached => slaActive
                ? q.Where(c => c.FollowUpStatus != FollowUpStatus.Completed && c.DueDate < today)
                : q.Where(c => false),
            DashboardCards.Sla => slaActive
                ? q.Where(c => c.FollowUpStatus == FollowUpStatus.Completed || c.DueDate >= today)
                : q,
            _ => q
        };
    }

    /// <summary>Breached first, then pending, completed last; newest within each group.</summary>
    private IOrderedQueryable<FeedbackCase> OrderForList(IQueryable<FeedbackCase> q, DateOnly today)
    {
        var slaActive = !_sla.IsWeekend(today);
        return q
            .OrderBy(c => slaActive && c.FollowUpStatus != FollowUpStatus.Completed && c.DueDate < today
                ? 0
                : c.FollowUpStatus == FollowUpStatus.Completed ? 2 : 1)
            .ThenByDescending(c => c.Date)
            .ThenByDescending(c => c.Id);
    }

    private async Task<List<DashboardCardViewModel>> BuildCardsSqlAsync(DashboardQuery query, DateOnly today, string selectedCard)
    {
        var cards = new List<DashboardCardViewModel>();
        foreach (var d in CardDefinitions())
        {
            cards.Add(new DashboardCardViewModel
            {
                Key = d.Key,
                Label = d.Label,
                Hint = d.Hint,
                Accent = d.Accent,
                Value = await ForCardSql(SqlFiltered(query), d.Key, today).CountAsync(),
                IsSelected = d.Key == selectedCard
            });
        }
        return cards;
    }

    private async Task<List<string>> MonthOptionsAsync()
    {
        var months = await _db.Cases.AsNoTracking()
            .Select(c => new { c.Date.Year, c.Date.Month })
            .Distinct()
            .ToListAsync();

        return months
            .Select(m => $"{m.Year:D4}-{m.Month:D2}")
            .OrderByDescending(s => s)
            .ToList();
    }

    // ---------- In-memory path (search / root-cause filters) ----------

    private static List<FeedbackCase> ApplyResidualFilters(List<FeedbackCase> cases, DashboardQuery query)
    {
        IEnumerable<FeedbackCase> result = cases;

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
                c.CaseNumber.ToString() == s ||
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

    private List<DashboardCardViewModel> BuildCardsInMemory(List<FeedbackCase> cases, DateOnly today, string selectedCard) =>
        CardDefinitions().Select(d => new DashboardCardViewModel
        {
            Key = d.Key,
            Label = d.Label,
            Hint = d.Hint,
            Accent = d.Accent,
            Value = ForCardInMemory(cases, d.Key, today).Count,
            IsSelected = d.Key == selectedCard
        }).ToList();

    private List<FeedbackCase> ForCardInMemory(List<FeedbackCase> cases, string card, DateOnly today) => card switch
    {
        DashboardCards.Pending => cases.Where(c => c.FollowUpStatus == FollowUpStatus.Pending).ToList(),
        DashboardCards.Sla => cases.Where(c => !_sla.IsBreached(c.DueDate, c.FollowUpStatus, today)).ToList(),
        DashboardCards.Breached => cases.Where(c => _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)).ToList(),
        DashboardCards.Completed => cases.Where(c => c.FollowUpStatus == FollowUpStatus.Completed).ToList(),
        DashboardCards.Valid => cases.Where(c => c.CaseValidation == CaseValidation.Valid).ToList(),
        DashboardCards.NotValid => cases.Where(c => c.CaseValidation == CaseValidation.NotValid).ToList(),
        _ => cases.ToList()
    };

    private List<FeedbackCase> SortInMemory(List<FeedbackCase> cases, DateOnly today) => cases
        .OrderBy(c => UrgencyRank(c, today))
        .ThenByDescending(c => c.Date)
        .ThenByDescending(c => c.Id)
        .ToList();

    /// <summary>0 = SLA breached and still open, 1 = pending, 2 = completed.</summary>
    private int UrgencyRank(FeedbackCase c, DateOnly today)
    {
        if (_sla.IsBreached(c.DueDate, c.FollowUpStatus, today)) return 0;
        return c.FollowUpStatus == FollowUpStatus.Completed ? 2 : 1;
    }

    // ---------- Shared ----------

    private static (string Key, string Label, string Hint, string Accent)[] CardDefinitions() => new[]
    {
        (DashboardCards.Total,     "Total",     "All cases matching the filters",       "accent-navy"),
        (DashboardCards.Pending,   "Pending",   "Follow-up not completed yet",          "accent-amber"),
        (DashboardCards.Sla,       "SLA",       "Cases still within their SLA",         "accent-green"),
        (DashboardCards.Breached,  "Breached",  "Follow-up past its due date",          "accent-red"),
        (DashboardCards.Completed, "Completed", "Follow-up completed",                  "accent-green"),
        (DashboardCards.Valid,     "Valid",     "Reviewed and marked valid",            "accent-indigo"),
        (DashboardCards.NotValid,  "Not valid", "Reviewed and marked not valid",        "accent-gray"),
    };

    private static int NormalizePage(int requested, int totalItems, out int page)
    {
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)DashboardQuery.PageSize));
        page = Math.Clamp(requested < 1 ? 1 : requested, 1, totalPages);
        return page;
    }

    private CaseRow ToRow(FeedbackCase c, DateOnly today) => new()
    {
        Case = c,
        AgeDays = today.DayNumber - c.Date.DayNumber,
        SlaBreached = _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)
    };
}
