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

    /// <summary>
    /// False when the manager roster is empty — the one case where the report
    /// genuinely has nobody to send to, and worth saying so plainly.
    /// </summary>
    public bool HasManagers => ManagerCount > 0;

    /// <summary>
    /// Shown on the page so a disabled Send button can be diagnosed at a glance:
    /// no managers is a Settings problem, managers with no assignments is a data
    /// problem, and neither line appearing at all means a stale build.
    /// </summary>
    public int ManagerCount { get; set; }
    public int AssignmentCount { get; set; }
}

public record RangeOption(string Value, string Label);
