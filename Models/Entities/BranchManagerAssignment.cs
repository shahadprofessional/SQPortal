using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>Which manager runs a branch. One row per branch, same as the SQ-side assignment.</summary>
public class BranchManagerAssignment
{
    [Key]
    [MaxLength(100)]
    public string BranchName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string AssignedManager { get; set; } = string.Empty;
}
