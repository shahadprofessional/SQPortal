using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Cases;

/// <summary>Read-only view of a single case.</summary>
public class CaseDetailsViewModel
{
    public FeedbackCase Case { get; set; } = null!;

    public int AgeDays { get; set; }
    public bool SlaBreached { get; set; }

    public string PartnerFullName { get; set; } = string.Empty;

    /// <summary>Recipient of the partner notification; empty disables the Send button.</summary>
    public string PartnerEmail { get; set; } = string.Empty;

    /// <summary>Post-action redirect target; already validated as a local URL.</summary>
    public string? ReturnUrl { get; set; }

    public List<string> RootCauses { get; set; } = new();

    /// <summary>This case's audit entries, oldest first, times in business time zone.</summary>
    public List<CaseHistoryItem> History { get; set; } = new();
}

public record CaseHistoryItem(DateTime Time, string User, string Action, string Details);
