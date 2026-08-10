using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>Which manager runs a branch; one row per branch.</summary>
public class BranchManagerAssignment
{
    [Key]
    [MaxLength(100)]
    public string BranchName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string AssignedManager { get; set; } = string.Empty;
}
