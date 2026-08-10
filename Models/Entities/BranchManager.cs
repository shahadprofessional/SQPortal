using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>Branch-side contact for a case; counterpart of <see cref="BusinessPartner"/>.</summary>
public class BranchManager
{
    [Key]
    [MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;
}
