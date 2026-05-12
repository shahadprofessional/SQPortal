using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class AnalyticsController : Controller
{
    private readonly AnalyticsService _service;

    public AnalyticsController(AnalyticsService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index(string? month, string? branch, string? partner)
    {
        var vm = await _service.BuildAsync(month, branch, partner, DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }
}
