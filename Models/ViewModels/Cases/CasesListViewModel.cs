using SQPortal.Models.Entities;
using SQPortal.Models.Enums;

namespace SQPortal.Models.ViewModels.Cases;

public class CasesListViewModel
{
    public IEnumerable<CaseRow> Cases { get; set; } = Array.Empty<CaseRow>();
    public IEnumerable<string> Months { get; set; } = Array.Empty<string>();
    public IEnumerable<string> RootCauseOptions { get; set; } = Array.Empty<string>();
    public int Count { get; set; }

    public string? Search { get; set; }
    public string? Month { get; set; }
    public FollowUpStatus? FollowUpStatus { get; set; }
    public string? RootCause { get; set; }

    public IReadOnlyCollection<string> FlaggedPhones { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> FlaggedNames { get; set; } = Array.Empty<string>();
    public IDictionary<string, string> PartnerEmails { get; set; } = new Dictionary<string, string>();
    public IDictionary<string, string> PartnerFullNames { get; set; } = new Dictionary<string, string>();
}

public class CaseRow
{
    public FeedbackCase Case { get; set; } = null!;
    public int AgeDays { get; set; }
    public bool SlaBreached { get; set; }
}
