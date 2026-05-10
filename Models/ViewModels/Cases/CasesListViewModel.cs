using SQPortal.Models.Entities;
using SQPortal.Models.Enums;

namespace SQPortal.Models.ViewModels.Cases;

public class CasesListViewModel
{
    public IEnumerable<FeedbackCase> Cases { get; set; } = Array.Empty<FeedbackCase>();
    public IEnumerable<string> Months { get; set; } = Array.Empty<string>();
    public int Count { get; set; }

    public string? Search { get; set; }
    public string? Month { get; set; }
    public FollowUpStatus? FollowUpStatus { get; set; }
    public string? RootCause { get; set; }
}
