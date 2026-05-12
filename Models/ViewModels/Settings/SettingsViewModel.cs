namespace SQPortal.Models.ViewModels.Settings;

public class SettingsViewModel
{
    public List<PartnerRow> Partners { get; set; } = new();
    public List<string> Branches { get; set; } = new();
    public List<BranchAssignmentRow> Assignments { get; set; } = new();
    public IDictionary<string, string> PartnerEmails { get; set; } = new Dictionary<string, string>();
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
