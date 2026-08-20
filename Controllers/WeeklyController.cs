using Microsoft.AspNetCore.Mvc;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class WeeklyController : Controller
{
    private readonly WeeklyReportService _service;
    private readonly EmailService _email;
    private readonly SlaService _sla;
    private readonly AuditService _audit;
    private readonly ILogger<WeeklyController> _logger;

    public WeeklyController(
        WeeklyReportService service,
        EmailService email,
        SlaService sla,
        AuditService audit,
        ILogger<WeeklyController> logger)
    {
        _service = service;
        _email = email;
        _sla = sla;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? range)
    {
        // Recipients come from the AD-sourced assignments as they stood over
        // the reported period; a branch AD names nobody for simply has no
        // recipient here.
        var vm = await _service.BuildAsync(range, _sla.Today);
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
        var vm = await _service.BuildAsync(range, _sla.Today);
        var item = vm.Branches.FirstOrDefault(b => b.Branch == branch);

        if (item == null)
        {
            TempData["StatusMessage"] = $"No report for \"{branch}\" in this period.";
        }
        else if (!item.CanSend)
        {
            TempData["StatusMessage"] = item.HasManager
                ? $"Active Directory has no email address for {item.ManagerNames}, so \"{item.Branch}\" cannot be sent to."
                : $"Active Directory names no branch manager for \"{item.Branch}\".";
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
        var vm = await _service.BuildAsync(range, _sla.Today);

        if (string.IsNullOrEmpty(vm.CombinedRecipients))
        {
            TempData["StatusMessage"] = "Nothing to send — no cases in this period, or Active Directory has no email for any of these branches' managers.";
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
            await _audit.LogAsync("Email sent", $"Weekly report \"{subject}\" to {recipients}");
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
