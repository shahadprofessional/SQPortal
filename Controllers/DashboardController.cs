using Microsoft.AspNetCore.Mvc;
using SQPortal.Models.ViewModels.Dashboard;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class DashboardController : Controller
{
    private readonly DashboardService _service;
    private readonly CsvExportService _csv;
    private readonly SlaService _sla;
    private readonly AuditService _audit;

    public DashboardController(DashboardService service, CsvExportService csv, SlaService sla, AuditService audit)
    {
        _service = service;
        _csv = csv;
        _sla = sla;
        _audit = audit;
    }

    public async Task<IActionResult> Index([FromQuery] DashboardQuery query)
    {
        var vm = await _service.BuildAsync(query, _sla.Today);
        return View(vm);
    }

    /// <summary>Exports the current card and filters, unpaged.</summary>
    [HttpGet]
    public async Task<IActionResult> ExportCsv([FromQuery] DashboardQuery query)
    {
        var cases = await _service.GetCasesAsync(query, _sla.Today);
        var bytes = _csv.Build(cases);

        // Exports carry customer PII out of the portal, so each one is audited.
        await _audit.LogAsync("Export", $"CSV export of {cases.Count} case{(cases.Count == 1 ? "" : "s")} (card: {DashboardCards.Normalize(query.Card)})");

        return File(bytes, "text/csv", $"cases-{_sla.Today:yyyyMMdd}.csv");
    }
}
