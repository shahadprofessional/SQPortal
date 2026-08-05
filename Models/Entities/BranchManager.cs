using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>
/// A branch manager — the branch side of a case, as opposed to the SQ side held
/// by <see cref="BusinessPartner"/>. Same shape: a short key plus contact details.
/// </summary>
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
