using SQPortal.Models.Entities;

namespace SQPortal.Data;

public static class LookupData
{
    // Branches, Partners and DefaultAssignments are development sample data,
    // seeded only when the environment is Development. UAT/production start
    // empty; real branches and staff are entered in Settings.
    // RootCauses is real configuration used by the app in every environment.
    public static readonly IReadOnlyList<string> Branches = new[]
    {
        "Northgate", "Eastfield", "Westbridge", "Southport", "Central Plaza",
        "Harbor", "Riverside", "Lakeshore", "Parkview", "Highland",
        "Meadowbrook", "Bayside", "Crossroads", "Summit", "Greenfield",
        "Stonebridge", "Oakwood", "Ridgefield", "Sunset", "Fairmount",
        "Brookline", "Hilltop", "Glenwood", "Maplewood", "Crestview",
        "Cedarfield", "Pinegrove"
    };

    public static readonly IReadOnlyList<BusinessPartner> Partners = new[]
    {
        new BusinessPartner { Name = "Nora",  FullName = "Nora Davies", Email = "nora@example.com" },
        new BusinessPartner { Name = "Ethan", FullName = "Ethan Hill",  Email = "ethan@example.com" },
        new BusinessPartner { Name = "Elena", FullName = "Elena Grant", Email = "elena@example.com" },
        new BusinessPartner { Name = "Diana", FullName = "Diana Shaw",  Email = "diana@example.com" }
    };

    public static readonly IReadOnlyList<string> RootCauses = new[]
    {
        "Staff Behavior",
        "Staff Knowledge",
        "Long Waiting Time",
        "Branch Facilities",
        "System / Technical Error",
        "Process Complexity",
        "Communication Issue"
    };

    public static IReadOnlyList<string> PartnerNames => Partners.Select(p => p.Name).ToList();

    public static string DefaultPartnerForBranch(string branch)
    {
        var index = Branches.ToList().IndexOf(branch);
        if (index < 0) return Partners[0].Name;
        return Partners[index % Partners.Count].Name;
    }

    public static IReadOnlyList<BranchPartnerAssignment> DefaultAssignments() =>
        Branches.Select(b => new BranchPartnerAssignment
        {
            BranchName = b,
            AssignedPartner = DefaultPartnerForBranch(b)
        }).ToList();
}
