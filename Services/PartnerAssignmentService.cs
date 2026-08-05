using Microsoft.EntityFrameworkCore;
using SQPortal.Data;

namespace SQPortal.Services;

public class PartnerAssignmentService
{
    private readonly SQPortalDbContext _db;

    public PartnerAssignmentService(SQPortalDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Who handles this branch, per Settings. Falls back to the first staff member
    /// on file rather than a hardcoded mapping — assignments are set in Settings,
    /// and an empty result simply means nobody has been added yet.
    /// </summary>
    public async Task<string> GetPartnerForBranchAsync(string branch)
    {
        var assignment = await _db.BranchAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.BranchName == branch);

        if (assignment != null) return assignment.AssignedPartner;

        return await _db.Partners
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => p.Name)
            .FirstOrDefaultAsync() ?? string.Empty;
    }
}
