namespace SQPortal.Services.Ad;

/// <summary>What one run of the AD sync did, for the log, the audit trail and Settings.</summary>
public class AdSyncResult
{
    public DateTime RanAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>False when AD is not linked yet; then nothing was read or written.</summary>
    public bool Linked { get; init; }

    /// <summary>False when a group could not be read; then nothing was written either.</summary>
    public bool Succeeded { get; init; }

    /// <summary>One line for Settings and the status message.</summary>
    public string Message { get; init; } = string.Empty;

    public int ManagersAdded { get; init; }
    public int ManagersUpdated { get; init; }
    public int ManagersDeactivated { get; init; }
    public int AssignmentsOpened { get; init; }
    public int AssignmentsClosed { get; init; }

    /// <summary>Things an administrator has to fix: a branch AD names but the portal does not, and such.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public int ChangeCount =>
        ManagersAdded + ManagersUpdated + ManagersDeactivated + AssignmentsOpened + AssignmentsClosed;

    public static AdSyncResult NotLinked(string message) => new()
    {
        Linked = false,
        Succeeded = false,
        Message = message
    };

    public static AdSyncResult Failed(string message, IReadOnlyList<string>? warnings = null) => new()
    {
        Linked = true,
        Succeeded = false,
        Message = message,
        Warnings = warnings ?? Array.Empty<string>()
    };
}

/// <summary>
/// The last sync result, kept in memory so Settings can show it. Nothing
/// depends on it surviving a restart: the roster itself lives in the database
/// and the background sync refills this within a minute of startup.
/// </summary>
public class AdSyncState
{
    private readonly object _gate = new();
    private AdSyncResult? _last;

    public AdSyncResult? Last
    {
        get { lock (_gate) { return _last; } }
        set { lock (_gate) { _last = value; } }
    }
}
