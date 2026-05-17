using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.ViewModels.Cases;

public class CaseFormViewModel
{
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [Required, StringLength(200)]
    [RegularExpression(@"^[A-Za-z ]+$", ErrorMessage = "Customer name may only contain letters and spaces.")]
    [Display(Name = "Customer name")]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^[0-9]{8}$", ErrorMessage = "Customer phone must be exactly 8 digits.")]
    [Display(Name = "Customer phone")]
    public string CustomerPhone { get; set; } = string.Empty;

    [StringLength(60)]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Ticket number may only contain digits.")]
    [Display(Name = "Ticket number")]
    public string? TicketNumber { get; set; }

    [Required, StringLength(100)]
    public string Branch { get; set; } = string.Empty;

    [Range(0, 5)]
    [Display(Name = "Branch rating")]
    public int BranchRating { get; set; }

    [RegularExpression(@"^[A-Za-z0-9 &+#]*$", ErrorMessage = "Branch comment may only contain letters, numbers, spaces, and the symbols & + #.")]
    [Display(Name = "Branch comment")]
    public string? BranchComment { get; set; }

    [StringLength(200)]
    [RegularExpression(@"^[A-Za-z ]*$", ErrorMessage = "Staff name may only contain letters and spaces.")]
    [Display(Name = "Staff name")]
    public string? StaffName { get; set; }

    [Range(0, 5)]
    [Display(Name = "Staff rating")]
    public int StaffRating { get; set; }

    [RegularExpression(@"^[A-Za-z0-9 &+#]*$", ErrorMessage = "Staff comment may only contain letters, numbers, spaces, and the symbols & + #.")]
    [Display(Name = "Staff comment")]
    public string? StaffComment { get; set; }

    public IEnumerable<string> Branches { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
    public IDictionary<string, string> BranchPartnerMap { get; set; } = new Dictionary<string, string>();
    public string? AssignedPartner { get; set; }
    public string? DuplicateWarning { get; set; }
}
