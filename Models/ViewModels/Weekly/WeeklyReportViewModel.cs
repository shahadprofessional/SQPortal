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

    /// <summary>False when Active Directory has given the portal no manager at all, so nothing can be sent.</summary>
    public bool HasManagers => ManagerCount > 0;

    /// <summary>Branch managers currently in the AD groups.</summary>
    public int ManagerCount { get; set; }

    /// <summary>Branches that had a manager over the reported period.</summary>
    public int AssignmentCount { get; set; }
}

public record RangeOption(string Value, string Label);
