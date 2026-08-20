using SQPortal.Models;
using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Weekly;

public class BranchReportItemViewModel
{
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// Who ran this branch over the reported period, from Active Directory.
    /// Usually one person; two while a handover overlaps. Empty when AD names
    /// nobody — the report is then shown but cannot be sent.
    /// </summary>
    public IReadOnlyList<BranchManagerContact> Managers { get; set; } = Array.Empty<BranchManagerContact>();

    /// <summary>The manager(s) by username, for the screen.</summary>
    public string ManagerNames => string.Join(", ", Managers.Select(m => m.Username));

    /// <summary>The manager(s) by full name, for addressing the email.</summary>
    public string ManagerDisplayNames => string.Join(" and ", Managers.Select(m => m.DisplayName));

    /// <summary>Recipient list: every manager of this branch who has an address in AD.</summary>
    public string ManagerEmail => string.Join(",", Managers
        .Where(m => m.HasEmail)
        .Select(m => m.Email)
        .Distinct(StringComparer.OrdinalIgnoreCase));

    public bool HasManager => Managers.Count > 0;

    /// <summary>
    /// True when someone named here has since dropped out of the AD groups —
    /// they ran the branch over this period but do not run it now.
    /// </summary>
    public bool HasFormerManager => Managers.Any(m => !m.IsActive);

    public IReadOnlyList<FeedbackCase> Cases { get; set; } = Array.Empty<FeedbackCase>();
    public IReadOnlyList<string> StaffMentioned { get; set; } = Array.Empty<string>();
    public int BranchFeedbackCount { get; set; }
    public int StaffFeedbackCount { get; set; }
    public int PoorCaseCount => Cases.Count;

    /// <summary>The email sent for this branch.</summary>
    public string EmailSubject { get; set; } = string.Empty;
    public string EmailBody { get; set; } = string.Empty;

    /// <summary>True when at least one manager email exists to send to.</summary>
    public bool CanSend => Managers.Any(m => m.HasEmail);

    public string PreviewText { get; set; } = string.Empty;
}
