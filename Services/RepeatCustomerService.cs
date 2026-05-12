using SQPortal.Models.Entities;
using SQPortal.Models.ViewModels.Dashboard;

namespace SQPortal.Services;

public class RepeatCustomerService
{
    private const int Threshold = 3;

    public IReadOnlyList<RepeatCustomerViewModel> Detect(IEnumerable<FeedbackCase> cases, DateOnly today)
    {
        var cutoff = today.AddYears(-1);
        var inWindow = cases.Where(c => c.Date >= cutoff).ToList();

        var byPhone = inWindow
            .Where(c => !string.IsNullOrWhiteSpace(c.CustomerPhone))
            .GroupBy(c => c.CustomerPhone.Trim())
            .Where(g => g.Count() >= Threshold)
            .ToList();

        var phoneFlagged = new HashSet<string>(byPhone.Select(g => g.Key), StringComparer.Ordinal);
        var nameFlaggedFromPhone = new HashSet<string>(
            byPhone.SelectMany(g => g).Select(c => (c.CustomerName ?? string.Empty).Trim().ToLowerInvariant()),
            StringComparer.Ordinal);

        var phoneResults = byPhone.Select(g =>
        {
            var first = g.OrderByDescending(c => c.Date).First();
            var label = string.IsNullOrWhiteSpace(first.CustomerName) ? g.Key : first.CustomerName.Trim();
            return new RepeatCustomerViewModel
            {
                Label = label,
                MatchType = "Phone",
                Sub = $"📞 {g.Key}",
                Count = g.Count()
            };
        });

        var byName = inWindow
            .Where(c => !string.IsNullOrWhiteSpace(c.CustomerName))
            .GroupBy(c => c.CustomerName.Trim().ToLowerInvariant())
            .Where(g => g.Count() >= Threshold && !nameFlaggedFromPhone.Contains(g.Key))
            .Select(g => new RepeatCustomerViewModel
            {
                Label = g.OrderByDescending(c => c.Date).First().CustomerName.Trim(),
                MatchType = "Name",
                Sub = "👤 Name match",
                Count = g.Count()
            });

        return phoneResults.Concat(byName)
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    public bool IsRepeat(FeedbackCase c, IReadOnlyCollection<string> flaggedPhones, IReadOnlyCollection<string> flaggedNames)
    {
        var phone = (c.CustomerPhone ?? string.Empty).Trim();
        if (!string.IsNullOrEmpty(phone) && flaggedPhones.Contains(phone)) return true;

        var name = (c.CustomerName ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(name) && flaggedNames.Contains(name)) return true;

        return false;
    }
}
