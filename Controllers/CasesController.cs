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

    public CasesController(
        SQPortalDbContext db,
        SlaService sla,
        PartnerAssignmentService partners)
    {
        _db = db;
        _sla = sla;
        _partners = partners;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkEmailSent(string id)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        if (!entity.EmailSent)
        {
            entity.EmailSent = true;
            await _db.SaveChangesAsync();
        }

        return Json(new { id, emailSent = true });
    }

    /// <summary>
    /// Single case, read-only. Returns just the panel when called with partial=1
    /// (the dashboard pulls it into a dialog), otherwise a full page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(string id, string? returnUrl = null, bool partial = false)
    {
        var entity = await _db.Cases.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var partner = await _db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == entity.BusinessPartner);

        var vm = new CaseDetailsViewModel
        {
            Case = entity,
            AgeDays = today.DayNumber - entity.Date.DayNumber,
            SlaBreached = _sla.IsBreached(entity.DueDate, entity.FollowUpStatus, today),
            PartnerFullName = partner?.FullName ?? string.Empty,
            PartnerEmail = partner?.Email ?? string.Empty,
            Mailto = partner == null ? string.Empty : DisplayHelpers.BuildLowRatingMailto(entity, partner.Email, partner.FullName),
            ReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null,
            RootCauses = entity.RootCauses.ToList()
        };

        return partial ? PartialView("_CaseDetails", vm) : View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CaseFormViewModel();
        await PopulateLookupsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CaseFormViewModel vm)
    {
        await PopulateLookupsAsync(vm);

        if (vm.Date > DateOnly.FromDateTime(DateTime.Today))
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
            CaseNumber = _db.NextCaseNumber(),
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

        _db.Cases.Add(entity);
        await _db.SaveChangesAsync();

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

        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Case updated.";
        return RedirectToAction("Index", "Dashboard");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, string? returnUrl = null)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        _db.Cases.Remove(entity);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Case deleted.";

        // Deleting from the dashboard list should land back on the same card/page.
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
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }
}
