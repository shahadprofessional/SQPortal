namespace SQPortal.Models.ViewModels.Settings;

public class SettingsViewModel
{
    /// <summary>One row per branch: name, current manager(s) and SQ owner.</summary>
    public List<BranchRow> Branches { get; set; } = new();

    /// <summary>The branch managers Active Directory knows about. Read-only here.</summary>
    public List<ManagerRow> Managers { get; set; } = new();

    /// <summary>SQ staff, still maintained in the portal.</summary>
    public List<PartnerRow> Partners { get; set; } = new();

    /// <summary>How the last AD sync went, and anything an administrator has to fix.</summary>
    public AdRosterStatus Ad { get; set; } = new();
}

public class PartnerRow
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// A branch manager exactly as Active Directory has them. Nothing on this row
/// is editable in the portal — that is the point.
/// </summary>
public class ManagerRow
{
    public string Username { get; set; } = string.Empty;
    public string UserPrincipalName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>False once AD no longer has them in any branch-manager group.</summary>
    public bool IsActive { get; set; }

    /// <summary>The branches they run today, per AD.</summary>
    public List<string> Branches { get; set; } = new();

    public DateTime? LastSyncedUtc { get; set; }
}

public class BranchRow
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Today's manager(s), from AD; empty when AD names nobody.</summary>
    public List<string> Managers { get; set; } = new();

    public string Partner { get; set; } = string.Empty;
}

/// <summary>The state of the link to Active Directory, for the managers panel.</summary>
public class AdRosterStatus
{
    /// <summary>False until a sync has been attempted since the app started.</summary>
    public bool HasRun { get; set; }

    public bool Linked { get; set; }
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>Last run in business time, already formatted; empty when it has not run.</summary>
    public string LastRun { get; set; } = string.Empty;

    public List<string> Warnings { get; set; } = new();
}
