using SQPortal.Models.ViewModels.Cases;

namespace SQPortal.Models.ViewModels.Dashboard;

public class DashboardViewModel
{
    public DateOnly Today { get; set; }

    public int Total { get; set; }
    public int Month { get; set; }
    public int Pending { get; set; }
    public int Breached { get; set; }
    public int Done { get; set; }
    public int Valid { get; set; }
    public int NotValid { get; set; }
    public int UnderReview { get; set; }
    public int StaffIssues { get; set; }
    public int BranchIssues { get; set; }

    public List<RepeatCustomerViewModel> RepeatCustomers { get; set; } = new();
    public List<CaseRow> Urgent { get; set; } = new();
    public List<CaseRow> Recent { get; set; } = new();

    public IDictionary<string, string> PartnerEmails { get; set; } = new Dictionary<string, string>();
    public IDictionary<string, string> PartnerFullNames { get; set; } = new Dictionary<string, string>();
    public IReadOnlyCollection<string> FlaggedPhones { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> FlaggedNames { get; set; } = Array.Empty<string>();
}
