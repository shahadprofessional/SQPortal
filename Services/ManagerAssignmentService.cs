using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;

namespace SQPortal.Services;

/// <summary>Keeps the rule "every branch has a branch manager" true in the data.</summary>
public class ManagerAssignmentService
{
    private readonly SQPortalDbContext _db;

    public ManagerAssignmentService(SQPortalDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Gives every branch an assignment row and repairs rows pointing at a
    /// deleted manager. Idempotent; no-op while the manager roster is empty.
    /// </summary>
    /// <returns>How many branches were filled in or repaired.</returns>
    public async Task<int> EnsureEveryBranchHasManagerAsync()
    {
        var managers = await _db.Managers
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .Select(m => m.Name)
            .ToListAsync();

        if (managers.Count == 0) return 0;

        var fallback = managers[0];
        var known = new HashSet<string>(managers, StringComparer.Ordinal);

        var branches = await _db.Branches.AsNoTracking().Select(b => b.Name).ToListAsync();
        var rows = await _db.ManagerAssignments.ToListAsync();
        var byBranch = rows.ToDictionary(a => a.BranchName, StringComparer.Ordinal);

        var touched = 0;
        foreach (var branch in branches)
        {
            if (!byBranch.TryGetValue(branch, out var row))
            {
                _db.ManagerAssignments.Add(new BranchManagerAssignment
                {
                    BranchName = branch,
                    AssignedManager = fallback
                });
                touched++;
            }
            else if (!known.Contains(row.AssignedManager))
            {
                row.AssignedManager = fallback;
                touched++;
            }
        }

        if (touched > 0) await _db.SaveChangesAsync();
        return touched;
    }
}
