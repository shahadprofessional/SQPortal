using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

public class BranchPartnerAssignment
{
    [Key]
    [MaxLength(100)]
    public string BranchName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string AssignedPartner { get; set; } = string.Empty;
}
