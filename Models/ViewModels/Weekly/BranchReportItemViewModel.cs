using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Weekly;

public class BranchReportItemViewModel
{
    public string Branch { get; set; } = string.Empty;

    /// <summary>The branch manager — the recipient of this report.</summary>
    public string Manager { get; set; } = string.Empty;
    public string ManagerFullName { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;

    public IReadOnlyList<FeedbackCase> Cases { get; set; } = Array.Empty<FeedbackCase>();
    public IReadOnlyList<string> StaffMentioned { get; set; } = Array.Empty<string>();
    public int BranchFeedbackCount { get; set; }
    public int StaffFeedbackCount { get; set; }
    public int PoorCaseCount => Cases.Count;

    /// <summary>Empty when the branch has no manager to send to.</summary>
    public string MailtoLink { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
}
