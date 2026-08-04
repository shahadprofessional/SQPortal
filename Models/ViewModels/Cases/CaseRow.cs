using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Cases;

/// <summary>One case as the dashboard list renders it.</summary>
public class CaseRow
{
    public FeedbackCase Case { get; set; } = null!;
    public int AgeDays { get; set; }
    public bool SlaBreached { get; set; }
}
