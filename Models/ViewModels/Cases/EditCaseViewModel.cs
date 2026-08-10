using System.ComponentModel.DataAnnotations;
using SQPortal.Helpers;
using SQPortal.Models.Enums;

namespace SQPortal.Models.ViewModels.Cases;

public class EditCaseViewModel : IValidatableObject
{
    [Required]
    public string Id { get; set; } = string.Empty;

    /// <summary>Display-only case number.</summary>
    public int CaseNumber { get; set; }

    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }

    [Required, StringLength(200)]
    [RegularExpression(FieldPatterns.Name, ErrorMessage = FieldPatterns.NameMessage)]
    [Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(FieldPatterns.Phone, ErrorMessage = FieldPatterns.PhoneMessage)]
    [Display(Name = "Customer phone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [StringLength(60)]
    [RegularExpression(FieldPatterns.AlphanumericOptional, ErrorMessage = FieldPatterns.AlphanumericMessage)]
    [Display(Name = "Ticket number")]
    public string? TicketNumber { get; set; }

    [Required, StringLength(100)]
    public string Branch { get; set; } = string.Empty;

    [Required, StringLength(40)]
    [Display(Name = "Business partner")]
    public string BusinessPartner { get; set; } = string.Empty;

    [Range(0, 5)]
    [Display(Name = "Branch rating")]
    public int BranchRating { get; set; }

    [RegularExpression(FieldPatterns.TextOptional, ErrorMessage = FieldPatterns.TextMessage)]
    [Display(Name = "Branch comment")]
    public string? BranchComment { get; set; }

    [StringLength(200)]
    [RegularExpression(FieldPatterns.NameOptional, ErrorMessage = FieldPatterns.NameMessage)]
    [Display(Name = "Staff name")]
    public string? StaffName { get; set; }

    [Range(0, 5)]
    [Display(Name = "Staff rating")]
    public int StaffRating { get; set; }

    [RegularExpression(FieldPatterns.TextOptional, ErrorMessage = FieldPatterns.TextMessage)]
    [Display(Name = "Staff comment")]
    public string? StaffComment { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Due date")]
    public DateOnly DueDate { get; set; }

    [Display(Name = "Follow-up status")]
    public FollowUpStatus FollowUpStatus { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Follow-up date")]
    public DateOnly? FollowUpDate { get; set; }

    [RegularExpression(FieldPatterns.TextOptional, ErrorMessage = FieldPatterns.TextMessage)]
    [Display(Name = "Follow-up notes")]
    public string? FollowUpNotes { get; set; }

    [Display(Name = "Case validation")]
    public CaseValidation CaseValidation { get; set; }

    [Display(Name = "Root causes")]
    public List<string> RootCauses { get; set; } = new();

    [RegularExpression(FieldPatterns.TextOptional, ErrorMessage = FieldPatterns.TextMessage)]
    [Display(Name = "Validation notes")]
    public string? ValidationNotes { get; set; }

    public IEnumerable<string> Branches { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
    public IEnumerable<string> RootCauseOptions { get; set; } = Array.Empty<string>();

    /// <summary>Due and follow-up dates cannot precede the case date.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (DueDate < Date)
        {
            yield return new ValidationResult(
                "Due date cannot be before the case date.",
                new[] { nameof(DueDate) });
        }

        if (FollowUpDate.HasValue && FollowUpDate.Value < Date)
        {
            yield return new ValidationResult(
                "Follow-up date cannot be before the case date.",
                new[] { nameof(FollowUpDate) });
        }
    }
}
