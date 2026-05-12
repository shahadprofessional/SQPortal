using System.Globalization;
using SQPortal.Models.ViewModels.Analytics;

namespace SQPortal.Models.ViewModels.Analytics;

public class AnalyticsViewModel
{
    public int Total { get; set; }
    public int Done { get; set; }
    public int Open { get; set; }
    public int Breached { get; set; }
    public int ResolutionRate { get; set; }
    public int SlaCompliance { get; set; }
    public double AvgBranchRating { get; set; }

    public List<BranchStats> ByBranch { get; set; } = new();
    public List<RootCauseStats> RootCauseDistribution { get; set; } = new();
    public List<PartnerStats> ByPartner { get; set; } = new();

    public string? Month { get; set; }
    public string? Branch { get; set; }
    public string? Partner { get; set; }

    public IEnumerable<string> Months { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Branches { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Partners { get; set; } = Array.Empty<string>();
}

public class BranchStats
{
    public string Name { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Done { get; set; }
    public int ResolutionRate { get; set; }
    public double AvgBranchRating { get; set; }
}

public class RootCauseStats
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public int Percent { get; set; }
}

public class PartnerStats
{
    public string Partner { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Done { get; set; }
    public int Pending { get; set; }
    public int SlaRate { get; set; }
}
