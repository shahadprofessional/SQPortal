using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Cases;

namespace SQPortal.Models.ViewModels.Dashboard;

public class DashboardViewModel
{
    public DateOnly Today { get; set; }

    /// <summary>Cards in display order; counts respect the active filters.</summary>
    public List<DashboardCardViewModel> Cards { get; set; } = new();

    /// <summary>Key of the card whose list is open (see <see cref="DashboardCards"/>).</summary>
    public string SelectedCard { get; set; } = DashboardCards.Total;
    public string SelectedCardLabel { get; set; } = string.Empty;

    /// <summary>The current page of records for the selected card.</summary>
    public List<CaseRow> Items { get; set; } = new();

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = DashboardQuery.PageSize;
    public int TotalItems { get; set; }
    public int TotalPages { get; set; } = 1;

    public int FirstItemOnPage => TotalItems == 0 ? 0 : ((Page - 1) * PageSize) + 1;
    public int LastItemOnPage => Math.Min(Page * PageSize, TotalItems);

    // Filters
    public string? Search { get; set; }
    public string? Month { get; set; }
    public FollowUpStatus? Status { get; set; }
    public string? RootCause { get; set; }
    public bool HasFilters =>
        !string.IsNullOrWhiteSpace(Search) ||
        !string.IsNullOrWhiteSpace(Month) ||
        Status.HasValue ||
        !string.IsNullOrWhiteSpace(RootCause);

    public IEnumerable<string> Months { get; set; } = Array.Empty<string>();
    public IEnumerable<string> RootCauseOptions { get; set; } = Array.Empty<string>();

    public IDictionary<string, string> PartnerEmails { get; set; } = new Dictionary<string, string>();
    public IDictionary<string, string> PartnerFullNames { get; set; } = new Dictionary<string, string>();
}
