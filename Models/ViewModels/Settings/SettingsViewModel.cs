namespace SQPortal.Models.ViewModels.Settings;

public class SettingsViewModel
{
    public List<PartnerRow> Partners { get; set; } = new();
    public List<string> Branches { get; set; } = new();
    public List<BranchAssignmentRow> Assignments { get; set; } = new();
    public IDictionary<string, string> PartnerEmails { get; set; } = new Dictionary<string, string>();

    /// <summary>The branch side: managers and which branch each one runs.</summary>
    public List<PartnerRow> Managers { get; set; } = new();
    public List<BranchManagerRow> ManagerAssignments { get; set; } = new();
}

public class PartnerRow
{
    public string Name { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class BranchAssignmentRow
{
    public string Branch { get; set; } = string.Empty;
    public string Partner { get; set; } = string.Empty;
    public string PartnerEmail { get; set; } = string.Empty;
}

public class BranchManagerRow
{
    public string Branch { get; set; } = string.Empty;
    public string Manager { get; set; } = string.Empty;
    public string ManagerEmail { get; set; } = string.Empty;
}
