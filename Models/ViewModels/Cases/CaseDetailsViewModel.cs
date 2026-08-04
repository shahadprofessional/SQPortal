using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Cases;

/// <summary>Read-only view of a single case — the same fields the edit form exposes.</summary>
public class CaseDetailsViewModel
{
    public FeedbackCase Case { get; set; } = null!;

    public int AgeDays { get; set; }
    public bool SlaBreached { get; set; }

    public string PartnerFullName { get; set; } = string.Empty;
    public string PartnerEmail { get; set; } = string.Empty;

    /// <summary>Prefilled partner mail link, or empty when the partner has no address on file.</summary>
    public string Mailto { get; set; } = string.Empty;

    /// <summary>Where Delete should land afterwards. Already checked as a local URL.</summary>
    public string? ReturnUrl { get; set; }

    public List<string> RootCauses { get; set; } = new();
    public string? OtherRootCause { get; set; }
}
