namespace SQPortal.Models.ViewModels.Weekly;

public class WeeklyReportViewModel
{
    public string Range { get; set; } = "thisWeek";
    public DateOnly Start { get; set; }
    public DateOnly End { get; set; }
    public List<BranchReportItemViewModel> Branches { get; set; } = new();
}
