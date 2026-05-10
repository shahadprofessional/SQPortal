using SQPortal.Models.Enums;

namespace SQPortal.Models.Entities;

public class FeedbackCase
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
    public FollowUpStatus FollowUpStatus { get; set; } = FollowUpStatus.Pending;
    public DateOnly? FollowUpDate { get; set; }
    public string? FollowUpNotes { get; set; }

    public CaseValidation CaseValidation { get; set; } = CaseValidation.UnderReview;
    public List<string> RootCauses { get; set; } = new();
    public string? ValidityStatus { get; set; }
    public string? ValidationNotes { get; set; }

    public bool EmailSent { get; set; }
}
