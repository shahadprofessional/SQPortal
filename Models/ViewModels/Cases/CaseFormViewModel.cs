namespace SQPortal.Models.ViewModels.Cases;

public class CaseFormViewModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string? TicketNumber { get; set; }

    public string Branch { get; set; } = string.Empty;

    public int BranchRating { get; set; }
    public string? BranchComment { get; set; }

    public string? StaffName { get; set; }
    public int StaffRating { get; set; }
    public string? StaffComment { get; set; }

    public IEnumerable<string> Branches { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
    public string? AssignedPartner { get; set; }
    public string? DuplicateWarning { get; set; }
}
