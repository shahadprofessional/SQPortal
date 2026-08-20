using System.ComponentModel.DataAnnotations;

namespace SQPortal.Models.Entities;

/// <summary>
/// Who ran a branch, and between which dates. One row per manager per spell,
/// written only by the AD sync.
///
/// Assignments are dated because managers move: when the AD group for
/// Westpark gains someone who used to run Southpark, the Southpark row is
/// closed off on the day before and a Westpark row opens on the day of the
/// change. Anything dated earlier still resolves to the manager who actually
/// held the branch then, so old reports keep saying what they always said.
///
/// The key is (branch, manager, start date); it is configured in
/// <c>SQPortalDbContext.OnModelCreating</c>.
/// </summary>
public class BranchManagerAssignment
{
    [MaxLength(100)]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>The manager's AD username (<see cref="BranchManager.Name"/>).</summary>
    [MaxLength(40)]
    public string AssignedManager { get; set; } = string.Empty;

    /// <summary>First day they ran this branch, inclusive.</summary>
    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Last day they ran it, inclusive; null while they still do.</summary>
    public DateOnly? EffectiveTo { get; set; }
}
