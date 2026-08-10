using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>One recorded action: who did what, when, to which case.</summary>
public class AuditEntry
{
    [Key]
    public long Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    [MaxLength(200)]
    public string User { get; set; } = string.Empty;

    [MaxLength(60)]
    public string Action { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? CaseId { get; set; }

    [MaxLength(400)]
    public string Details { get; set; } = string.Empty;
}
