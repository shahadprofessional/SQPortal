namespace SQPortal.Services.Ad;

/// <summary>
/// Where the branch-manager identities come from. Bound from the "Ad" section
/// of appsettings.json. The portal never edits any of this in Active
/// Directory; it only reads it.
/// </summary>
public class AdSettings
{
    public const string SectionName = "Ad";

    /// <summary>
    /// False until IT Security has handed over the real group names. While it
    /// is false the portal keeps whatever the last sync wrote and reports
    /// "not linked" in Settings instead of inventing managers.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Domain controller or domain name to query, e.g. "contoso.local". Empty
    /// means the machine's own domain, which is the normal case on a
    /// domain-joined server.
    /// </summary>
    public string Server { get; set; } = string.Empty;

    /// <summary>
    /// Base DN the group lookups start from, e.g.
    /// "DC=contoso,DC=local". Empty means the domain root.
    /// </summary>
    public string SearchBase { get; set; } = string.Empty;

    /// <summary>
    /// The UPN suffix the branch managers' usernames carry in AD (the part
    /// from the "@" onwards). Placeholder until IT Security confirms the real
    /// domain; used to spot members that came from somewhere unexpected.
    /// </summary>
    public string UsernameSuffix { get; set; } = "@changeMeLater";

    /// <summary>How often the background sync re-reads the groups. Minimum 5.</summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// One entry per AD group that holds branch managers: the group says which
    /// branch its members manage. Roughly six of them, named alphabetically;
    /// the real names come from IT Security.
    /// </summary>
    public List<AdBranchGroup> BranchGroups { get; set; } = new();

    /// <summary>
    /// True when the section is filled in well enough to query: enabled, and
    /// at least one group mapped to a branch.
    /// </summary>
    public bool IsLinked => Enabled && ConfiguredGroups().Count > 0;

    /// <summary>The entries that name both a branch and a group; the rest are ignored.</summary>
    public IReadOnlyList<AdBranchGroup> ConfiguredGroups() =>
        BranchGroups
            .Where(g => !string.IsNullOrWhiteSpace(g.Branch) && !string.IsNullOrWhiteSpace(g.Group))
            .Select(g => new AdBranchGroup { Branch = g.Branch.Trim(), Group = g.Group.Trim() })
            .ToList();

    /// <summary>Sync interval, floored at five minutes so a typo cannot hammer the domain controller.</summary>
    public TimeSpan SyncInterval =>
        TimeSpan.FromMinutes(SyncIntervalMinutes < 5 ? 5 : SyncIntervalMinutes);
}

/// <summary>An AD group and the branch its members manage.</summary>
public class AdBranchGroup
{
    /// <summary>Branch name exactly as it is spelled in the portal's branch list.</summary>
    public string Branch { get; set; } = string.Empty;

    /// <summary>
    /// AD group name — sAMAccountName ("SQ-BranchManagers-A") or full
    /// distinguished name.
    /// </summary>
    public string Group { get; set; } = string.Empty;
}
