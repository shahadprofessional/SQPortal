using System.ComponentModel.DataAnnotations;
using SQPortal.Models.Enums;

namespace SQPortal.Models.Entities;

public class FeedbackCase
{
    [Key]
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    /// <summary>Human-facing case number, assigned on create and never reused.</summary>
    public int CaseNumber { get; set; }

    public DateOnly Date { get; set; }

    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string CustomerPhone { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? TicketNumber { get; set; }

    [MaxLength(100)]
    public string Branch { get; set; } = string.Empty;

    [MaxLength(40)]
    public string BusinessPartner { get; set; } = string.Empty;

    public int BranchRating { get; set; }
    public string? BranchComment { get; set; }

    [MaxLength(200)]
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

    /// <summary>Soft-delete flag; deleted cases are hidden by a global query filter, never removed.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Optimistic-concurrency token (SQL rowversion); a stale edit fails instead of overwriting.</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
