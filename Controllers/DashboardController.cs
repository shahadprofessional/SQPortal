using Microsoft.AspNetCore.Mvc;
using SQPortal.Models.ViewModels.Dashboard;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class DashboardController : Controller
{
    private readonly DashboardService _service;

    public DashboardController(DashboardService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index([FromQuery] DashboardQuery query)
    {
        var vm = await _service.BuildAsync(query, DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }
}
