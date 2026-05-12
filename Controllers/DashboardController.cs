using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class DashboardController : Controller
{
    private readonly DashboardService _service;

    public DashboardController(DashboardService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var vm = await _service.BuildAsync(DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }
}
