namespace SQPortal.Models.ViewModels.Settings;

public class SettingsViewModel
{
    public List<BranchAssignmentRow> Rows { get; set; } = new();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
    public IDictionary<string, string> PartnerEmails { get; set; } = new Dictionary<string, string>();
}

public class BranchAssignmentRow
{
    public string Branch { get; set; } = string.Empty;
    public string Partner { get; set; } = string.Empty;
    public string PartnerEmail { get; set; } = string.Empty;
}
