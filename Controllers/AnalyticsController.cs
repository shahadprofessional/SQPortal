using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class AnalyticsController : Controller
{
    private readonly AnalyticsService _service;
    private readonly SlaService _sla;

    public AnalyticsController(AnalyticsService service, SlaService sla)
    {
        _service = service;
        _sla = sla;
    }

    public async Task<IActionResult> Index(string? month, string? branch, string? partner)
    {
        var vm = await _service.BuildAsync(month, branch, partner, _sla.Today);
        return View(vm);
    }
}
