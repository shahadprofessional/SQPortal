using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Audit;

public class AuditViewModel
{
    public List<AuditEntry> Items { get; set; } = new();
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
}
