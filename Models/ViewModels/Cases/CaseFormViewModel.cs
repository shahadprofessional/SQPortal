using System.ComponentModel.DataAnnotations;
using SQPortal.Helpers;

namespace SQPortal.Models.ViewModels.Cases;

public class CaseFormViewModel
{
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

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

    public IEnumerable<string> Branches { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
    public IDictionary<string, string> BranchPartnerMap { get; set; } = new Dictionary<string, string>();
    public string? AssignedPartner { get; set; }
}
