using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Weekly;

public class BranchReportItemViewModel
{
    public string Branch { get; set; } = string.Empty;
    public string Partner { get; set; } = string.Empty;
    public IEnumerable<FeedbackCase> Cases { get; set; } = Array.Empty<FeedbackCase>();
    public IEnumerable<string> StaffMentioned { get; set; } = Array.Empty<string>();
    public int PoorCaseCount { get; set; }
}
