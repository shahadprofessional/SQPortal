using SQPortal.Models.Enums;

namespace SQPortal.Models.ViewModels.Dashboard;

/// <summary>Dashboard query-string parameters: selected card, filters and page.</summary>
public class DashboardQuery
{
    public const int PageSize = 25;

    public string? Card { get; set; }
    public string? Search { get; set; }
    public string? Month { get; set; }
    public FollowUpStatus? Status { get; set; }
    public string? RootCause { get; set; }
    public int Page { get; set; } = 1;
}
