using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SQPortal.Data;
using SQPortal.Helpers;
using SQPortal.Models.Entities;

namespace SQPortal.Services.Ad;

/// <summary>
/// Copies the branch managers Active Directory names into the portal: who they
/// are, how to reach them, and which branch each one runs.
///
/// The portal is not where any of this is decided. When someone stops managing
/// a branch, moves to another one or leaves the company, that is done in AD and
/// this sync follows it. Assignments are dated rather than overwritten, so a
/// move closes the old branch's row the day before and opens the new branch's
/// row on the day of the change: everything already recorded against the old
/// branch keeps naming the manager who actually held it then.
/// </summary>
public class AdManagerSyncService
{
    private readonly SQPortalDbContext _db;
    private readonly IAdDirectoryReader _directory;
    private readonly AdSettings _settings;
    private readonly SlaService _sla;
    private readonly AuditService _audit;
    private readonly AdSyncState _state;
    private readonly ILogger<AdManagerSyncService> _logger;

    public AdManagerSyncService(
        SQPortalDbContext db,
        IAdDirectoryReader directory,
        IOptions<AdSettings> settings,
        SlaService sla,
        AuditService audit,
        AdSyncState state,
        ILogger<AdManagerSyncService> logger)
    {
        _db = db;
        _directory = directory;
        _settings = settings.Value;
        _sla = sla;
        _audit = audit;
        _state = state;
        _logger = logger;
    }

    /// <summary>The last run's outcome, for Settings; null before the first run.</summary>
    public AdSyncResult? LastResult => _state.Last;

    /// <summary>
    /// Reads every configured group and writes what changed. A group that
    /// cannot be read aborts the whole run without touching the database — a
    /// directory that is briefly unreachable must never look like "no branch
    /// has a manager any more".
    /// </summary>
    /// <param name="triggeredByUser">
    /// Audit-trail actor; null uses the signed-in user, which is right for the
    /// Settings button and wrong for the background run.
    /// </param>
    public async Task<AdSyncResult> SyncAsync(string? triggeredByUser = null, CancellationToken cancellationToken = default)
    {
        var groups = _settings.ConfiguredGroups();

        if (!_settings.Enabled)
        {
            return Remember(AdSyncResult.NotLinked(
                "Active Directory is not linked yet — set Ad:Enabled in appsettings.json once IT Security has given you the group names."));
        }

        if (groups.Count == 0)
        {
            return Remember(AdSyncResult.NotLinked(
                "Ad:Enabled is on but no branch-manager group is configured — fill in Ad:BranchGroups with the group names and the branch each one manages."));
        }

        if (!_directory.IsLinked)
        {
            return Remember(AdSyncResult.NotLinked(
                "The Active Directory reader is not switched on — follow the AD markers in SQPortal.csproj, Services/Ad/LdapDirectoryReader.cs and Program.cs."));
        }

        var warnings = new List<string>();

        // Read everything first. Writing must not start until every group has
        // been read, so a half-read directory cannot half-empty the roster.
        var membersByGroup = new Dictionary<string, IReadOnlyList<AdUser>>(StringComparer.OrdinalIgnoreCase);
        foreach (var groupName in groups.Select(g => g.Group).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                membersByGroup[groupName] = await _directory.GetGroupMembersAsync(groupName, cancellationToken);
            }
            catch (AdDirectoryException ex)
            {
                _logger.LogError(ex, "AD sync aborted while reading group {Group}", groupName);
                return Remember(AdSyncResult.Failed(
                    $"Nothing was changed — Active Directory could not be read: {ex.Message}"));
            }
        }

        var branchNames = await _db.Branches.AsNoTracking().Select(b => b.Name).ToListAsync(cancellationToken);
        var portalBranches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var branch in branchNames) portalBranches.TryAdd(branch, branch);

        // What AD says, keyed by the portal's own spelling of the branch.
        var desired = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var everyone = new Dictionary<string, AdUser>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in groups)
        {
            var members = membersByGroup[mapping.Group];

            // Membership makes someone a branch manager even when the branch
            // itself is not on file yet, so their details still land in the roster.
            foreach (var member in members) everyone[member.Username] = member;

            if (!portalBranches.TryGetValue(mapping.Branch, out var branch))
            {
                warnings.Add(
                    $"AD group \"{mapping.Group}\" is mapped to branch \"{mapping.Branch}\", which is not in the portal's branch list — " +
                    "add the branch in Settings, or correct the name in Ad:BranchGroups.");
                continue;
            }

            if (!desired.TryGetValue(branch, out var wanted))
            {
                wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                desired[branch] = wanted;
            }

            foreach (var member in members) wanted.Add(member.Username);

            if (members.Count == 0)
            {
                warnings.Add($"AD group \"{mapping.Group}\" has no enabled member, so \"{branch}\" has no branch manager.");
            }
        }

        var now = DateTime.UtcNow;
        var managersAdded = 0;
        var managersUpdated = 0;
        var managersDeactivated = 0;

        var roster = await _db.Managers.ToListAsync(cancellationToken);
        var byUsername = new Dictionary<string, BranchManager>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in roster) byUsername.TryAdd(row.Name, row);

        // AD's spelling of a username can differ in case from the stored one;
        // the stored spelling stays the key so assignment rows keep matching.
        var canonicalUsername = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in everyone.Values)
        {
            var username = user.Username.Trim();
            if (username.Length == 0) continue;

            if (username.Length > 40)
            {
                warnings.Add($"AD username \"{username}\" is longer than the 40 characters the portal stores — skipped.");
                continue;
            }

            var upn = Truncate(user.UserPrincipalName.Trim(), 200);
            var fullName = Truncate(user.FullName.Trim(), 200);
            var email = Truncate(user.Email.Trim(), 200);

            var suffix = (_settings.UsernameSuffix ?? string.Empty).Trim();
            if (suffix.Length > 0 && upn.Length > 0 && !upn.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add($"\"{username}\" signs in as \"{upn}\", which does not end with the configured Ad:UsernameSuffix \"{suffix}\".");
            }

            if (email.Length == 0)
            {
                warnings.Add($"Active Directory has no email address for \"{username}\" — their branch report cannot be sent.");
            }
            else if (!DisplayHelpers.IsValidEmailAddress(email))
            {
                warnings.Add($"Active Directory's email for \"{username}\" (\"{email}\") is not a valid address — their branch report cannot be sent.");
            }

            if (byUsername.TryGetValue(username, out var existing))
            {
                canonicalUsername[username] = existing.Name;

                var changed = existing.UserPrincipalName != upn
                           || existing.FullName != fullName
                           || existing.Email != email
                           || !existing.IsActive;

                existing.UserPrincipalName = upn;
                existing.FullName = fullName;
                existing.Email = email;
                existing.IsActive = true;
                existing.LastSyncedUtc = now;

                if (changed) managersUpdated++;
            }
            else
            {
                canonicalUsername[username] = username;
                _db.Managers.Add(new BranchManager
                {
                    Name = username,
                    UserPrincipalName = upn,
                    FullName = fullName,
                    Email = email,
                    IsActive = true,
                    LastSyncedUtc = now
                });
                managersAdded++;
            }
        }

        // In none of the groups any more: moved on or left. The row stays, so
        // the branches they used to run still resolve to a person.
        foreach (var row in roster)
        {
            if (everyone.ContainsKey(row.Name) || !row.IsActive) continue;
            row.IsActive = false;
            managersDeactivated++;
        }

        var today = _sla.Today;
        var yesterday = today.AddDays(-1);
        var assignmentsOpened = 0;
        var assignmentsClosed = 0;

        var openRows = await _db.ManagerAssignments
            .Where(a => a.EffectiveTo == null)
            .ToListAsync(cancellationToken);

        // Spells that already started today may have been closed earlier the
        // same day; they are reopened rather than inserted twice.
        var startedToday = await _db.ManagerAssignments
            .Where(a => a.EffectiveFrom == today)
            .ToListAsync(cancellationToken);

        foreach (var (branch, wanted) in desired)
        {
            var open = openRows
                .Where(r => string.Equals(r.BranchName, branch, StringComparison.Ordinal))
                .ToList();

            foreach (var row in open)
            {
                if (wanted.Contains(row.AssignedManager)) continue;

                if (row.EffectiveFrom >= today)
                {
                    // Opened today and already gone: it covered no day at all,
                    // so it leaves no history behind.
                    _db.ManagerAssignments.Remove(row);
                }
                else
                {
                    row.EffectiveTo = yesterday;
                }

                assignmentsClosed++;
            }

            foreach (var member in wanted)
            {
                var username = canonicalUsername.TryGetValue(member, out var known) ? known : member;

                if (open.Any(r => string.Equals(r.AssignedManager, username, StringComparison.OrdinalIgnoreCase)
                               && r.EffectiveTo == null))
                {
                    continue;
                }

                var reopened = startedToday.FirstOrDefault(r =>
                    string.Equals(r.BranchName, branch, StringComparison.Ordinal)
                    && string.Equals(r.AssignedManager, username, StringComparison.OrdinalIgnoreCase));

                if (reopened != null)
                {
                    reopened.EffectiveTo = null;
                }
                else
                {
                    _db.ManagerAssignments.Add(new BranchManagerAssignment
                    {
                        BranchName = branch,
                        AssignedManager = username,
                        EffectiveFrom = today,
                        EffectiveTo = null
                    });
                }

                assignmentsOpened++;
            }
        }

        foreach (var branch in openRows.Select(r => r.BranchName).Distinct(StringComparer.Ordinal))
        {
            if (desired.ContainsKey(branch)) continue;
            warnings.Add(
                $"No AD group is configured for branch \"{branch}\" — its manager is still whoever was last recorded. " +
                "Add the branch's group to Ad:BranchGroups.");
        }

        foreach (var branch in branchNames)
        {
            if (desired.ContainsKey(branch) || openRows.Any(r => string.Equals(r.BranchName, branch, StringComparison.Ordinal)))
            {
                continue;
            }
            warnings.Add($"Branch \"{branch}\" has no AD group and no manager on file.");
        }

        var result = new AdSyncResult
        {
            RanAtUtc = now,
            Linked = true,
            Succeeded = true,
            ManagersAdded = managersAdded,
            ManagersUpdated = managersUpdated,
            ManagersDeactivated = managersDeactivated,
            AssignmentsOpened = assignmentsOpened,
            AssignmentsClosed = assignmentsClosed,
            Warnings = warnings,
            Message = Summarize(groups.Count, managersAdded, managersUpdated, managersDeactivated, assignmentsOpened, assignmentsClosed)
        };

        if (result.ChangeCount > 0)
        {
            _audit.Log("AD sync", result.Message, userOverride: triggeredByUser);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (result.ChangeCount > 0)
        {
            _logger.LogInformation("AD sync: {Summary}", result.Message);
        }
        foreach (var warning in warnings)
        {
            _logger.LogWarning("AD sync: {Warning}", warning);
        }

        return Remember(result);
    }

    private AdSyncResult Remember(AdSyncResult result)
    {
        _state.Last = result;
        if (!result.Succeeded)
        {
            _logger.LogWarning("AD sync did not run: {Message}", result.Message);
        }
        return result;
    }

    private static string Summarize(int groupCount, int added, int updated, int deactivated, int opened, int closed)
    {
        var parts = new List<string>();
        if (added > 0) parts.Add($"{added} manager{Plural(added)} added");
        if (updated > 0) parts.Add($"{updated} updated");
        if (deactivated > 0) parts.Add($"{deactivated} no longer managing");
        if (opened > 0) parts.Add($"{opened} branch assignment{Plural(opened)} opened");
        if (closed > 0) parts.Add($"{closed} closed");

        return parts.Count == 0
            ? $"Read {groupCount} AD group{Plural(groupCount)}; the portal already matched Active Directory."
            : $"Read {groupCount} AD group{Plural(groupCount)}: {string.Join(", ", parts)}.";
    }

    private static string Plural(int count) => count == 1 ? string.Empty : "s";

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
