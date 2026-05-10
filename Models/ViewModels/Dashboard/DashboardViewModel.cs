using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Dashboard;

public class DashboardViewModel
{
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
    public List<FeedbackCase> Urgent { get; set; } = new();
    public List<FeedbackCase> Recent { get; set; } = new();
}
