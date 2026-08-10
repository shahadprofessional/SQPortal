using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Weekly;

public class BranchReportItemViewModel
{
    public string Branch { get; set; } = string.Empty;

    /// <summary>The branch manager, recipient of this report.</summary>
    public string Manager { get; set; } = string.Empty;
    public string ManagerFullName { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;

    public IReadOnlyList<FeedbackCase> Cases { get; set; } = Array.Empty<FeedbackCase>();
    public IReadOnlyList<string> StaffMentioned { get; set; } = Array.Empty<string>();
    public int BranchFeedbackCount { get; set; }
    public int StaffFeedbackCount { get; set; }
    public int PoorCaseCount => Cases.Count;

    /// <summary>The email sent for this branch.</summary>
    public string EmailSubject { get; set; } = string.Empty;
    public string EmailBody { get; set; } = string.Empty;

    /// <summary>True when a manager email exists to send to.</summary>
    public bool CanSend => !string.IsNullOrWhiteSpace(ManagerEmail);

    public string PreviewText { get; set; } = string.Empty;
}
