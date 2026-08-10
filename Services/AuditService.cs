using SQPortal.Data;
using SQPortal.Models.Entities;

namespace SQPortal.Services;

/// <summary>Writes audit entries recording who did what, when, to which case.</summary>
public class AuditService
{
    private readonly SQPortalDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditService(SQPortalDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    /// <summary>
    /// Queues an audit entry without saving, so a caller's SaveChangesAsync
    /// commits the entry and the audited change together. userOverride covers
    /// sign-in, where the acting user is not on the request principal yet.
    /// </summary>
    public void Log(string action, string details, string? caseId = null, string? userOverride = null)
    {
        _db.Audits.Add(new AuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            User = userOverride ?? _http.HttpContext?.User?.Identity?.Name ?? "unknown",
            Action = action,
            CaseId = caseId,
            Details = details.Length <= 400 ? details : details[..400]
        });
    }

    /// <summary>Queues and saves immediately, for actions with no save of their own.</summary>
    public async Task LogAsync(string action, string details, string? caseId = null, string? userOverride = null)
    {
        Log(action, details, caseId, userOverride);
        await _db.SaveChangesAsync();
    }
}
