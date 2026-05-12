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
    public string CombinedMailtoLink { get; set; } = string.Empty;
}

public record RangeOption(string Value, string Label);
