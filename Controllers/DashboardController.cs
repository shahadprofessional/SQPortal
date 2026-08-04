using Microsoft.AspNetCore.Mvc;
using SQPortal.Models.ViewModels.Dashboard;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class DashboardController : Controller
{
    private readonly DashboardService _service;
    private readonly CsvExportService _csv;

    public DashboardController(DashboardService service, CsvExportService csv)
    {
        _service = service;
        _csv = csv;
    }

    public async Task<IActionResult> Index([FromQuery] DashboardQuery query)
    {
        var vm = await _service.BuildAsync(query, DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }

    /// <summary>Exports the list as shown — same card, same filters, every page.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv([FromQuery] DashboardQuery query)
    {
        var cases = await _service.GetCasesAsync(query, DateOnly.FromDateTime(DateTime.Today));
        var bytes = _csv.Build(cases);
        return File(bytes, "text/csv", $"cases-{DateTime.Today:yyyyMMdd}.csv");
    }
}
