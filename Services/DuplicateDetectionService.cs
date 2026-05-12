using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Enums;

namespace SQPortal.Services;

public class DuplicateDetectionService
{
    private readonly SQPortalDbContext _db;

    public DuplicateDetectionService(SQPortalDbContext db)
    {
        _db = db;
    }

    public async Task<string?> CheckOpenDuplicateAsync(string customerPhone)
    {
        var phone = (customerPhone ?? string.Empty).Trim();
        if (phone.Length < 7) return null;

        var oneYearAgo = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));

        var match = await _db.Cases
            .AsNoTracking()
            .Where(c => c.CustomerPhone == phone
                        && c.FollowUpStatus != FollowUpStatus.Completed
                        && c.Date >= oneYearAgo)
            .OrderByDescending(c => c.Date)
            .FirstOrDefaultAsync();

        return match == null
            ? null
            : $"Open case already exists for this phone (Branch: {match.Branch}, Date: {match.Date:yyyy-MM-dd}).";
    }
}
