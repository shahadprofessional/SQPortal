using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class WeeklyController : Controller
{
    private readonly WeeklyReportService _service;

    public WeeklyController(WeeklyReportService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(string? range)
    {
        var vm = await _service.BuildAsync(range, DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }
}
