using SQPortal.Models.Enums;

namespace SQPortal.Models.ViewModels.Cases;

public class EditCaseViewModel
{
    public string Id { get; set; } = string.Empty;
    public DateOnly Date { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? TicketNumber { get; set; }

    public string Branch { get; set; } = string.Empty;
    public string BusinessPartner { get; set; } = string.Empty;

    public int BranchRating { get; set; }
    public string? BranchComment { get; set; }

    public string? StaffName { get; set; }
    public int StaffRating { get; set; }
    public string? StaffComment { get; set; }

    public DateOnly DueDate { get; set; }
    public FollowUpStatus FollowUpStatus { get; set; }
    public DateOnly? FollowUpDate { get; set; }
    public string? FollowUpNotes { get; set; }

    public CaseValidation CaseValidation { get; set; }
    public List<string> RootCauses { get; set; } = new();
    public string? OtherRootCause { get; set; }
    public string? ValidationNotes { get; set; }

    public IEnumerable<string> Branches { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
}
