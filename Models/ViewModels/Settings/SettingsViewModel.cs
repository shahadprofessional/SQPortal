using SQPortal.Models.Entities;

namespace SQPortal.Models.ViewModels.Settings;

public class SettingsViewModel
{
    public List<BranchPartnerAssignment> Assignments { get; set; } = new();
    public IEnumerable<BusinessPartner> Partners { get; set; } = Array.Empty<BusinessPartner>();
}
