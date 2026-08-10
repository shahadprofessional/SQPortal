using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class WeeklyController : Controller
{
    private readonly WeeklyReportService _service;
    private readonly ManagerAssignmentService _managers;
    private readonly EmailService _email;
    private readonly ILogger<WeeklyController> _logger;

    public WeeklyController(
        WeeklyReportService service,
        ManagerAssignmentService managers,
        EmailService email,
        ILogger<WeeklyController> logger)
    {
        _service = service;
        _managers = managers;
        _email = email;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? range)
    {
        // Ensure every branch has a manager assignment before resolving recipients.
        await _managers.EnsureEveryBranchHasManagerAsync();

        var vm = await _service.BuildAsync(range, DateOnly.FromDateTime(DateTime.Today));
        return View(vm);
    }

    /// <summary>
    /// Emails one branch's report to its manager from the configured mailbox.
    /// The report content is rebuilt server-side, never taken from the request.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendBranch(string? range, string branch)
    {
        var vm = await _service.BuildAsync(range, DateOnly.FromDateTime(DateTime.Today));
        var item = vm.Branches.FirstOrDefault(b => b.Branch == branch);

        if (item == null)
        {
            TempData["StatusMessage"] = $"No report for \"{branch}\" in this period.";
        }
        else if (!item.CanSend)
        {
            TempData["StatusMessage"] = $"\"{item.Branch}\" has no manager email on file — add one in Settings.";
        }
        else
        {
            await TrySendAsync(item.ManagerEmail, item.EmailSubject, item.EmailBody,
                $"Report for \"{item.Branch}\" sent to {item.ManagerEmail} from {_email.FromAddress}.");
        }

        return RedirectToAction(nameof(Index), new { range = vm.Range });
    }

    /// <summary>One combined mail to every manager in the current report.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendCombined(string? range)
    {
        var vm = await _service.BuildAsync(range, DateOnly.FromDateTime(DateTime.Today));

        if (string.IsNullOrEmpty(vm.CombinedRecipients))
        {
            TempData["StatusMessage"] = "Nothing to send — no cases in this period, or no manager has an email on file.";
        }
        else
        {
            await TrySendAsync(vm.CombinedRecipients, vm.CombinedSubject, vm.CombinedPreviewText,
                $"Combined report sent to {vm.CombinedRecipients} from {_email.FromAddress}.");
        }

        return RedirectToAction(nameof(Index), new { range = vm.Range });
    }

    private async Task TrySendAsync(string recipients, string subject, string body, string successMessage)
    {
        try
        {
            await _email.SendAsync(recipients, subject, body);
            TempData["StatusMessage"] = successMessage;
        }
        catch (InvalidOperationException ex)
        {
            // Configuration errors carry a user-safe message.
            TempData["StatusMessage"] = ex.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sending weekly report \"{Subject}\" failed", subject);
            TempData["StatusMessage"] = "The email could not be sent — check the Mail settings in appsettings.json.";
        }
    }
}
