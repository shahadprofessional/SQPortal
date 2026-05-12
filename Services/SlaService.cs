using SQPortal.Models.Enums;

namespace SQPortal.Services;

public class SlaService
{
    public DateOnly NextWorkingDay(DateOnly from)
    {
        var d = from.AddDays(1);
        while (d.DayOfWeek == DayOfWeek.Friday || d.DayOfWeek == DayOfWeek.Saturday)
        {
            d = d.AddDays(1);
        }
        return d;
    }

    public bool IsBreached(DateOnly dueDate, FollowUpStatus status, DateOnly today)
    {
        if (status == FollowUpStatus.Completed) return false;
        if (today.DayOfWeek == DayOfWeek.Friday || today.DayOfWeek == DayOfWeek.Saturday) return false;
        return dueDate < today;
    }
}
