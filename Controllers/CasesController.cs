using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Helpers;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Cases;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class CasesController : Controller
{
    private readonly SQPortalDbContext _db;
    private readonly SlaService _sla;
    private readonly PartnerAssignmentService _partners;
    private readonly EmailService _email;
    private readonly AuditService _audit;
    private readonly ILogger<CasesController> _logger;

    public CasesController(
        SQPortalDbContext db,
        SlaService sla,
        PartnerAssignmentService partners,
        EmailService email,
        AuditService audit,
        ILogger<CasesController> logger)
    {
        _db = db;
        _sla = sla;
        _partners = partners;
        _email = email;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Emails the low-rating notification to the case's partner from the
    /// configured mailbox. EmailSent is set only after a successful send.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPartnerEmail(string id, string? returnUrl = null)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == entity.BusinessPartner);

        if (partner == null || string.IsNullOrWhiteSpace(partner.Email))
        {
            TempData["StatusMessage"] = "No partner email on file for this case.";
        }
        else
        {
            var (subject, body) = DisplayHelpers.BuildLowRatingEmail(entity, partner.FullName);
            try
            {
                await _email.SendAsync(partner.Email, subject, body);
                entity.EmailSent = true;
                _audit.Log("Email sent", $"Case #{entity.CaseNumber}: partner notification to {partner.Email}", entity.Id);
                await _db.SaveChangesAsync();
                TempData["StatusMessage"] = $"Notification sent to {partner.Email} from {_email.FromAddress}.";
            }
            catch (InvalidOperationException ex)
            {
                // Configuration errors carry a user-safe message.
                TempData["StatusMessage"] = ex.Message;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sending partner email for case {CaseId} failed", entity.Id);
                TempData["StatusMessage"] = "The email could not be sent — check the Mail settings in appsettings.json.";
            }
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    /// <summary>
    /// Single case, read-only. partial=1 returns only the panel for the
    /// dashboard dialog; otherwise a full page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(string id, string? returnUrl = null, bool partial = false)
    {
        var entity = await _db.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        var today = _sla.Today;
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == entity.BusinessPartner);

        var auditEntries = await _db.Audits.AsNoTracking()
            .Where(a => a.CaseId == entity.Id)
            .OrderBy(a => a.Id)
            .ToListAsync();

        var vm = new CaseDetailsViewModel
        {
            Case = entity,
            AgeDays = today.DayNumber - entity.Date.DayNumber,
            SlaBreached = _sla.IsBreached(entity.DueDate, entity.FollowUpStatus, today),
            PartnerFullName = partner?.FullName ?? string.Empty,
            PartnerEmail = partner?.Email ?? string.Empty,
            ReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            RootCauses = entity.RootCauses.ToList(),
            History = auditEntries
                .Select(a => new CaseHistoryItem(_sla.ToBusinessTime(a.TimestampUtc), a.User, a.Action, a.Details))
                .ToList()
        };

        return partial ? PartialView("_CaseDetails", vm) : View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CaseFormViewModel { Date = _sla.Today, Today = _sla.Today };
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CaseFormViewModel vm)
    {
        vm.Today = _sla.Today;
        await PopulateLookupsAsync(vm);

        if (vm.Date > _sla.Today)
        {
            ModelState.AddModelError(nameof(vm.Date), "Date cannot be in the future.");
        }

        if (vm.BranchRating == 0 && vm.StaffRating == 0)
        {
            ModelState.AddModelError(string.Empty, "At least one rating (branch or staff) is required.");
        }

        if (!ModelState.IsValid)
        {
            vm.AssignedPartner = await _partners.GetPartnerForBranchAsync(vm.Branch);
            return View(vm);
        }

        var partner = await _partners.GetPartnerForBranchAsync(vm.Branch);
        var entity = new FeedbackCase
        {
            Id = GenerateId(),
            Date = vm.Date,
            CustomerName = vm.CustomerName.Trim(),
            CustomerPhone = vm.CustomerPhone.Trim(),
            TicketNumber = string.IsNullOrWhiteSpace(vm.TicketNumber) ? null : vm.TicketNumber.Trim(),
            Branch = vm.Branch,
            BusinessPartner = partner,
            BranchRating = vm.BranchRating,
            BranchComment = vm.BranchComment,
            StaffName = vm.StaffName,
            StaffRating = vm.StaffRating,
            StaffComment = vm.StaffComment,
            DueDate = _sla.NextWorkingDay(vm.Date),
            FollowUpStatus = FollowUpStatus.Pending,
            CaseValidation = CaseValidation.UnderReview
        };

        // A concurrent create can claim the same MAX+1 number; the unique index
        // rejects the duplicate and the retry reads a fresh number.
        for (var attempt = 0; ; attempt++)
        {
            entity.CaseNumber = _db.NextCaseNumber();
            try
            {
                _db.Cases.Add(entity);
                await _db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                _db.Entry(entity).State = EntityState.Detached;
            }
        }

        await _audit.LogAsync("Case created", $"Case #{entity.CaseNumber} for {entity.CustomerName} at {entity.Branch}", entity.Id);

        TempData["StatusMessage"] = "Case created.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        var (branches, partners) = await GetBranchesAndPartnersAsync();

        var vm = new EditCaseViewModel
        {
            Id = entity.Id,
            CaseNumber = entity.CaseNumber,
            Date = entity.Date,
            CustomerName = entity.CustomerName,
            CustomerPhone = entity.CustomerPhone,
            TicketNumber = entity.TicketNumber,
            Branch = entity.Branch,
            BusinessPartner = entity.BusinessPartner,
            BranchRating = entity.BranchRating,
            BranchComment = entity.BranchComment,
            StaffName = entity.StaffName,
            StaffRating = entity.StaffRating,
            StaffComment = entity.StaffComment,
            DueDate = entity.DueDate,
            FollowUpStatus = entity.FollowUpStatus,
            FollowUpDate = entity.FollowUpDate,
            FollowUpNotes = entity.FollowUpNotes,
            CaseValidation = entity.CaseValidation,
            RootCauses = entity.RootCauses.Where(r => LookupData.RootCauses.Contains(r)).ToList(),
            ValidationNotes = entity.ValidationNotes,
            RowVersion = Convert.ToBase64String(entity.RowVersion ?? Array.Empty<byte>()),
            Branches = branches,
            Partners = partners,
            RootCauseOptions = LookupData.RootCauses
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditCaseViewModel vm)
    {
        if (!ModelState.IsValid)
        {
            var (branches, partners) = await GetBranchesAndPartnersAsync();
            vm.Branches = branches;
            vm.Partners = partners;
            vm.RootCauseOptions = LookupData.RootCauses;
            return View(vm);
        }

        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == vm.Id);
        if (entity == null) return NotFound();

        var rootCauses = vm.RootCauses?.Where(r => LookupData.RootCauses.Contains(r)).ToList() ?? new();

        // Old values, for the per-case history entries below.
        var oldStatus = entity.FollowUpStatus;
        var oldValidation = entity.CaseValidation;
        var oldPartner = entity.BusinessPartner;

        entity.Date = vm.Date;
        entity.CustomerName = vm.CustomerName.Trim();
        entity.CustomerPhone = vm.CustomerPhone.Trim();
        entity.TicketNumber = string.IsNullOrWhiteSpace(vm.TicketNumber) ? null : vm.TicketNumber.Trim();
        entity.Branch = vm.Branch;
        entity.BusinessPartner = vm.BusinessPartner;
        entity.BranchRating = vm.BranchRating;
        entity.BranchComment = vm.BranchComment;
        entity.StaffName = vm.StaffName;
        entity.StaffRating = vm.StaffRating;
        entity.StaffComment = vm.StaffComment;
        entity.DueDate = vm.DueDate;
        entity.FollowUpStatus = vm.FollowUpStatus;
        entity.FollowUpDate = vm.FollowUpDate;
        entity.FollowUpNotes = vm.FollowUpNotes;
        entity.CaseValidation = vm.CaseValidation;
        entity.RootCauses = rootCauses;
        entity.ValidityStatus = rootCauses.FirstOrDefault();
        entity.ValidationNotes = vm.ValidationNotes;

        // The form carries the row version it was loaded with; a save against a
        // row someone changed in the meantime fails instead of overwriting.
        if (!string.IsNullOrEmpty(vm.RowVersion))
        {
            try
            {
                _db.Entry(entity).Property(e => e.RowVersion).OriginalValue = Convert.FromBase64String(vm.RowVersion);
            }
            catch (FormatException)
            {
                // A mangled token falls back to last-write-wins.
            }
        }

        // Status, validation and reassignment get their own history entries in
        // the site's naming; anything else logs as a plain edit.
        var loggedSpecific = false;
        if (oldStatus != entity.FollowUpStatus)
        {
            _audit.Log("Follow-up", $"Follow-up: {oldStatus} → {entity.FollowUpStatus}", entity.Id);
            loggedSpecific = true;
        }
        if (oldValidation != entity.CaseValidation)
        {
            _audit.Log("Validation",
                $"Validation: {DisplayHelpers.ValidationLabel(oldValidation)} → {DisplayHelpers.ValidationLabel(entity.CaseValidation)}",
                entity.Id);
            loggedSpecific = true;
        }
        if (!string.Equals(oldPartner, entity.BusinessPartner, StringComparison.Ordinal))
        {
            _audit.Log("Reassigned", $"SQ staff: {oldPartner} → {entity.BusinessPartner}", entity.Id);
            loggedSpecific = true;
        }
        if (!loggedSpecific)
        {
            _audit.Log("Case updated", $"Case #{entity.CaseNumber} edited", entity.Id);
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            ModelState.AddModelError(string.Empty,
                "This case was changed by someone else while the form was open. Reload the page and reapply the edits.");

            var (branches, partners) = await GetBranchesAndPartnersAsync();
            vm.Branches = branches;
            vm.Partners = partners;
            vm.RootCauseOptions = LookupData.RootCauses;
            return View(vm);
        }

        TempData["StatusMessage"] = "Case updated.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, string? returnUrl = null)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        // Soft delete: the row is hidden everywhere but stays recoverable, and
        // the audit trail records who removed it.
        entity.IsDeleted = true;
        _audit.Log("Case deleted", $"Case #{entity.CaseNumber} for {entity.CustomerName} at {entity.Branch}", entity.Id);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Case deleted.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }


    private async Task PopulateLookupsAsync(CaseFormViewModel vm)
    {
        vm.Branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).Select(b => b.Name).ToListAsync();
        vm.Partners = await _db.Partners.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).ToListAsync();
        vm.BranchPartnerMap = await _db.BranchAssignments
            .AsNoTracking()
            .ToDictionaryAsync(a => a.BranchName, a => a.AssignedPartner);
    }

    private async Task<(List<string> Branches, List<string> Partners)> GetBranchesAndPartnersAsync()
    {
        var branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).Select(b => b.Name).ToListAsync();
        var partners = await _db.Partners.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).ToListAsync();
        return (branches, partners);
    }

    private static string GenerateId()
    {
        // Unix-ms prefix keeps IDs in creation order (used as a sort tie-break);
        // the random suffix prevents guessing and same-millisecond collisions.
        var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        return $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{suffix}";
    }
}
