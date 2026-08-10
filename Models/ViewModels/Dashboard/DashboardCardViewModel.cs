namespace SQPortal.Models.ViewModels.Dashboard;

/// <summary>Keys for the dashboard cards; the key selects which slice of cases the list shows.</summary>
public static class DashboardCards
{
    public const string Total = "total";
    public const string Pending = "pending";
    public const string Sla = "sla";
    public const string Breached = "breached";
    public const string Completed = "completed";
    public const string Valid = "valid";
    public const string NotValid = "notvalid";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Total, Pending, Sla, Breached, Completed, Valid, NotValid
    };

    /// <summary>Unknown keys fall back to <see cref="Total"/>.</summary>
    public static string Normalize(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return Total;
        var normalized = key.Trim().ToLowerInvariant();
        return All.Contains(normalized) ? normalized : Total;
    }
}

public class DashboardCardViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Hint { get; set; } = string.Empty;
    public int Value { get; set; }
    public string Accent { get; set; } = "accent-navy";
    public bool IsSelected { get; set; }
}
