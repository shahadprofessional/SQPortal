namespace SQPortal.Services;

/// <summary>SLA clock configuration, bound from the "Sla" section of appsettings.json.</summary>
public class SlaSettings
{
    public const string SectionName = "Sla";

    /// <summary>
    /// Time-zone id "today" is computed in (e.g. "Asia/Bahrain" or
    /// "Arabian Standard Time"). Empty means the server's local zone, which is
    /// wrong on cloud servers running UTC — set it explicitly for hosting.
    /// </summary>
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>Non-working days for SLA purposes.</summary>
    public string[] WeekendDays { get; set; } = { "Friday", "Saturday" };
}
