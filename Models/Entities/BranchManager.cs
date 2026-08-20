using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>
/// Branch-side contact for a case; counterpart of <see cref="BusinessPartner"/>.
/// Every field is a copy of what Active Directory says — the portal reflects
/// the roster, it never edits it, so nobody can retype a manager's name or
/// email here.
/// </summary>
public class BranchManager
{
    /// <summary>AD sAMAccountName; the key the assignments point at.</summary>
    [Key]
    [MaxLength(40)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Full username including the domain suffix (AD userPrincipalName).</summary>
    [MaxLength(200)]
    public string UserPrincipalName { get; set; } = string.Empty;

    /// <summary>AD displayName.</summary>
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    /// <summary>AD mail attribute.</summary>
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// False once they are in none of the branch-manager groups — they moved
    /// on or resigned. The row stays so past assignments still resolve to a
    /// name and address.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>When a sync last saw this person in AD.</summary>
    public DateTime? LastSyncedUtc { get; set; }
}
