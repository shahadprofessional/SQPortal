using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class WeeklyController : Controller
{
    private readonly WeeklyReportService _service;
    private readonly ManagerAssignmentService _managers;

    public WeeklyController(WeeklyReportService service, ManagerAssignmentService managers)
    {
        _service = service;
        _managers = managers;
    }

    public async Task<IActionResult> Index(string? range)
    {
        // Every branch has a manager, so make sure the data says so before we go
        // looking for recipients — otherwise a branch added before any manager
        // existed would have no one to send to.
        await _managers.EnsureEveryBranchHasManagerAsync();

        var vm = await _service.BuildAsync(range, DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }
}
