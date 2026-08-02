using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.Enums;
using SQPortal.Models.ViewModels.Cases;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class CasesController : Controller
{
    private const string OtherRootCausePrefix = "Other: ";

    private readonly SQPortalDbContext _db;
    private readonly SlaService _sla;
    private readonly PartnerAssignmentService _partners;
    private readonly DuplicateDetectionService _duplicates;
    private readonly CsvExportService _csv;
    private readonly RepeatCustomerService _repeats;

    public CasesController(
        SQPortalDbContext db,
        SlaService sla,
        PartnerAssignmentService partners,
        DuplicateDetectionService duplicates,
        CsvExportService csv,
        RepeatCustomerService repeats)
    {
        _db = db;
        _sla = sla;
        _partners = partners;
        _duplicates = duplicates;
        _csv = csv;
        _repeats = repeats;
    }

    public async Task<IActionResult> Index(string? search, string? month, FollowUpStatus? followUpStatus, string? rootCause)
    {
        var cases = await ApplyFiltersAsync(search, month, followUpStatus, rootCause);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var rows = cases.Select(c => new CaseRow
        {
            Case = c,
            AgeDays = (today.DayNumber - c.Date.DayNumber),
            SlaBreached = _sla.IsBreached(c.DueDate, c.FollowUpStatus, today)
        }).ToList();

        var months = await _db.Cases
            .AsNoTracking()
            .Select(c => c.Date)
            .ToListAsync();

        var monthOptions = months
            .Select(d => $"{d.Year:D4}-{d.Month:D2}")
            .Distinct()
            .OrderByDescending(s => s)
            .ToList();

        var allCasesForFlagging = await _db.Cases.AsNoTracking().ToListAsync();
        var flagged = _repeats.Detect(allCasesForFlagging, today);
        var flaggedPhones = new HashSet<string>(
            flagged.Where(f => f.MatchType == "Phone")
                   .Select(f => f.Sub.Replace("📞 ", string.Empty).Trim()),
            StringComparer.Ordinal);
        var flaggedNames = new HashSet<string>(
            flagged.Where(f => f.MatchType == "Name")
                   .Select(f => f.Label.Trim().ToLowerInvariant()),
            StringComparer.Ordinal);

        var dbPartners = await _db.Partners.AsNoTracking().ToListAsync();

        var vm = new CasesListViewModel
        {
            Cases = rows,
            Count = rows.Count,
            Months = monthOptions,
            RootCauseOptions = LookupData.RootCauses,
            Search = search,
            Month = month,
            FollowUpStatus = followUpStatus,
            RootCause = rootCause,
            FlaggedPhones = flaggedPhones,
            FlaggedNames = flaggedNames,
            PartnerEmails = dbPartners.ToDictionary(p => p.Name, p => p.Email),
            PartnerFullNames = dbPartners.ToDictionary(p => p.Name, p => p.FullName)
        };

        return View(vm);
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
            vm.DuplicateWarning = await _duplicates.CheckOpenDuplicateAsync(vm.CustomerPhone);
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

        _db.Cases.Add(entity);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Case created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        var (selected, other) = SplitRootCauses(entity.RootCauses);
        var (branches, partners) = await GetBranchesAndPartnersAsync();

        var vm = new EditCaseViewModel
        {
            Id = entity.Id,
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
            RootCauses = selected,
            OtherRootCause = other,
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
        if (!string.IsNullOrWhiteSpace(vm.OtherRootCause))
        {
            rootCauses.Add(OtherRootCausePrefix + vm.OtherRootCause.Trim());
        }

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
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var entity = await _db.Cases.FirstOrDefaultAsync(c => c.Id == id);
        if (entity == null) return NotFound();

        _db.Cases.Remove(entity);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Case deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv(string? search, string? month, FollowUpStatus? followUpStatus, string? rootCause)
    {
        var cases = await ApplyFiltersAsync(search, month, followUpStatus, rootCause);
        var bytes = _csv.Build(cases);
        var fileName = $"cases-{DateTime.Today:yyyyMMdd}.csv";
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> CheckDuplicate(string phone)
    {
        var warning = await _duplicates.CheckOpenDuplicateAsync(phone ?? string.Empty);
        return Json(new { warning });
    }

    private async Task<List<FeedbackCase>> ApplyFiltersAsync(string? search, string? month, FollowUpStatus? followUpStatus, string? rootCause)
    {
        var query = _db.Cases.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.CustomerName, $"%{s}%") ||
                EF.Functions.Like(c.CustomerPhone, $"%{s}%") ||
                EF.Functions.Like(c.Branch, $"%{s}%") ||
                (c.StaffName != null && EF.Functions.Like(c.StaffName, $"%{s}%")) ||
                (c.TicketNumber != null && EF.Functions.Like(c.TicketNumber, $"%{s}%")));
        }

        if (!string.IsNullOrWhiteSpace(month)
            && DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
        {
            var start = DateOnly.FromDateTime(monthDate);
            var end = start.AddMonths(1);
            query = query.Where(c => c.Date >= start && c.Date < end);
        }

        if (followUpStatus.HasValue)
        {
            query = query.Where(c => c.FollowUpStatus == followUpStatus.Value);
        }

        var results = await query.OrderByDescending(c => c.Date).ThenByDescending(c => c.Id).ToListAsync();

        if (!string.IsNullOrWhiteSpace(rootCause))
        {
            results = rootCause == "Other"
                ? results.Where(c => c.RootCauses.Any(r => r.StartsWith(OtherRootCausePrefix))).ToList()
                : results.Where(c => c.RootCauses.Contains(rootCause)).ToList();
        }

        return results;
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

    private static (List<string> selected, string? other) SplitRootCauses(IEnumerable<string> rootCauses)
    {
        var selected = new List<string>();
        string? other = null;
        foreach (var r in rootCauses)
        {
            if (r.StartsWith(OtherRootCausePrefix))
            {
                other = r.Substring(OtherRootCausePrefix.Length);
            }
            else
            {
                selected.Add(r);
            }
        }
        return (selected, other);
    }

    private static string GenerateId()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
    }
}
