using Microsoft.Extensions.Options;
using SQPortal.Models.Enums;

namespace SQPortal.Services;

public class SlaService
{
    private readonly TimeZoneInfo _timeZone;
    private readonly HashSet<DayOfWeek> _weekend;

    public SlaService(IOptions<SlaSettings> options)
    {
        var settings = options.Value;

        _timeZone = ResolveTimeZone(settings.TimeZone);

        _weekend = settings.WeekendDays
            .Select(d => Enum.TryParse<DayOfWeek>(d, ignoreCase: true, out var day) ? (DayOfWeek?)day : null)
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .ToHashSet();
        if (_weekend.Count == 0)
        {
            _weekend = new HashSet<DayOfWeek> { DayOfWeek.Friday, DayOfWeek.Saturday };
        }
    }

    /// <summary>Today's date in the configured business time zone.</summary>
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, _timeZone));

    public bool IsWeekend(DateOnly date) => _weekend.Contains(date.DayOfWeek);

    public DateOnly NextWorkingDay(DateOnly from)
    {
        var d = from.AddDays(1);
        while (_weekend.Contains(d.DayOfWeek))
        {
            d = d.AddDays(1);
        }
        return d;
    }

    public bool IsBreached(DateOnly dueDate, FollowUpStatus status, DateOnly today)
    {
        if (status == FollowUpStatus.Completed) return false;
        if (IsWeekend(today)) return false;
        return dueDate < today;
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.Local;
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneInfo.Local;
        }
    }
}
