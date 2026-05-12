using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Weekly;

public class BranchReportItemViewModel
{
    public string Branch { get; set; } = string.Empty;
    public string Partner { get; set; } = string.Empty;
    public string PartnerEmail { get; set; } = string.Empty;
    public IReadOnlyList<FeedbackCase> Cases { get; set; } = Array.Empty<FeedbackCase>();
    public IReadOnlyList<string> StaffMentioned { get; set; } = Array.Empty<string>();
    public int BranchFeedbackCount { get; set; }
    public int StaffFeedbackCount { get; set; }
    public int PoorCaseCount => Cases.Count;
    public string MailtoLink { get; set; } = string.Empty;
    public string PreviewText { get; set; } = string.Empty;
}
