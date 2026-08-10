using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.ViewModels.Audit;

namespace SQPortal.Controllers;

public class AuditController : Controller
{
    private const int PageSize = 50;

    private readonly SQPortalDbContext _db;

    public AuditController(SQPortalDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        var totalItems = await _db.Audits.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page < 1 ? 1 : page, 1, totalPages);

        var items = await _db.Audits.AsNoTracking()
            .OrderByDescending(a => a.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return View(new AuditViewModel
        {
            Items = items,
            Page = page,
            TotalPages = totalPages,
            TotalItems = totalItems
        });
    }
}
