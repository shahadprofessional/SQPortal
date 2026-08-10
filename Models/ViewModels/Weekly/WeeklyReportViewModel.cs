namespace SQPortal.Models.ViewModels.Weekly;

public class WeeklyReportViewModel
{
    public string Range { get; set; } = "thisWeek";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public List<BranchReportItemViewModel> Branches { get; set; } = new();
    public int TotalPoorCases { get; set; }
    public IReadOnlyList<RangeOption> RangeOptions { get; set; } = Array.Empty<RangeOption>();
    public string CombinedPreviewText { get; set; } = string.Empty;

    /// <summary>The one combined mail: every manager in the report, each address once.</summary>
    public string CombinedSubject { get; set; } = string.Empty;
    public string CombinedRecipients { get; set; } = string.Empty;

    /// <summary>False when the manager roster is empty and the report has no possible recipient.</summary>
    public bool HasManagers => ManagerCount > 0;

    public int ManagerCount { get; set; }
    public int AssignmentCount { get; set; }
}

public record RangeOption(string Value, string Label);
