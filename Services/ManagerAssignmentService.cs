using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models;

namespace SQPortal.Services;

/// <summary>
/// Answers "who ran this branch on this date" from the dated assignment rows
/// the AD sync writes.
///
/// Read-only on purpose: a branch without a manager in AD stays without one
/// here. Handing it to whoever happens to be first on the roster would put a
/// name on a report that Active Directory never said was responsible.
/// </summary>
public class ManagerAssignmentService
{
    private readonly SQPortalDbContext _db;

    public ManagerAssignmentService(SQPortalDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Every branch that had a manager on <paramref name="date"/>, with the
    /// people who ran it then. Branches with nobody are absent from the map.
    /// </summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlyList<BranchManagerContact>>> GetManagersAsOfAsync(DateOnly date)
    {
        var rows = await _db.ManagerAssignments
            .AsNoTracking()
            .Where(a => a.EffectiveFrom <= date && (a.EffectiveTo == null || a.EffectiveTo >= date))
            .Select(a => new { a.BranchName, a.AssignedManager })
            .ToListAsync();

        if (rows.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<BranchManagerContact>>(StringComparer.Ordinal);
        }

        var usernames = rows.Select(r => r.AssignedManager).Distinct(StringComparer.Ordinal).ToList();
        var people = await _db.Managers
            .AsNoTracking()
            .Where(m => usernames.Contains(m.Name))
            .ToDictionaryAsync(m => m.Name, StringComparer.Ordinal);

        return rows
            .GroupBy(r => r.BranchName, StringComparer.Ordinal)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<BranchManagerContact>)g
                    .Select(r => people.TryGetValue(r.AssignedManager, out var person)
                        ? new BranchManagerContact(person.Name, person.FullName, person.Email, person.IsActive)
                        // The roster row is gone but the history says they ran
                        // the branch; the username is still the honest answer.
                        : new BranchManagerContact(r.AssignedManager, string.Empty, string.Empty, false))
                    .OrderBy(c => c.Username, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);
    }
}
