namespace SQPortal.Models.ViewModels.Analytics;

public class AnalyticsViewModel
{
    public int Total { get; set; }
    public double ResolutionRate { get; set; }
    public double SlaCompliance { get; set; }
    public double AvgBranchRating { get; set; }
    public int Breached { get; set; }
    public int Open { get; set; }

    public Dictionary<string, BranchStats> ByBranch { get; set; } = new();
    public Dictionary<string, int> RootCauseDistribution { get; set; } = new();
    public List<PartnerStats> ByPartner { get; set; } = new();

    public string? Month { get; set; }
    public string? Branch { get; set; }
    public string? Partner { get; set; }
}

public class BranchStats
{
    public int Total { get; set; }
    public int Done { get; set; }
    public double ResolutionRate { get; set; }
    public double AvgRating { get; set; }
}

public class PartnerStats
{
    public string Partner { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Done { get; set; }
    public int Pending { get; set; }
    public double SlaRate { get; set; }
}
