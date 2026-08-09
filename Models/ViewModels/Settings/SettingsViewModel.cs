namespace SQPortal.Models.ViewModels.Settings;

public class SettingsViewModel
{
    /// <summary>One row per branch: its name, its manager and its SQ owner.</summary>
    public List<BranchRow> Branches { get; set; } = new();

    /// <summary>Rosters, used for the dropdowns as well as their own editors.</summary>
    public List<PartnerRow> Managers { get; set; } = new();
    public List<PartnerRow> Partners { get; set; } = new();
}

public class PartnerRow
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class BranchRow
{
    public string Name { get; set; } = string.Empty;
    public string Manager { get; set; } = string.Empty;
    public string Partner { get; set; } = string.Empty;
}
