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

    public async Task<string> GetPartnerForBranchAsync(string branch)
    {
        var assignment = await _db.BranchAssignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.BranchName == branch);

        return assignment?.AssignedPartner ?? LookupData.DefaultPartnerForBranch(branch);
    }
}
