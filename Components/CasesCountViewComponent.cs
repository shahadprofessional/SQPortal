using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;

namespace SQPortal.Components;

public class CasesCountViewComponent : ViewComponent
{
    private readonly SQPortalDbContext _db;

    public CasesCountViewComponent(SQPortalDbContext db)
    {
        _db = db;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var count = await _db.Cases.CountAsync();
        return View(count);
    }
}
