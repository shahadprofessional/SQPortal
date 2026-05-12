using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Models.Entities;
using SQPortal.Models.ViewModels.Settings;

namespace SQPortal.Controllers;

public class SettingsController : Controller
{
    private readonly SQPortalDbContext _db;

    public SettingsController(SQPortalDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var partnerEmails = LookupData.Partners.ToDictionary(p => p.Name, p => p.Email);
        var assignments = await _db.BranchAssignments
            .AsNoTracking()
            .ToDictionaryAsync(a => a.BranchName, a => a.AssignedPartner);

        var rows = LookupData.Branches
            .Select(b =>
            {
                var partner = assignments.TryGetValue(b, out var p)
                    ? p
                    : LookupData.DefaultPartnerForBranch(b);
                return new BranchAssignmentRow
                {
                    Branch = b,
                    Partner = partner,
                    PartnerEmail = partnerEmails.TryGetValue(partner, out var e) ? e : string.Empty
                };
            })
            .ToList();

        var vm = new SettingsViewModel
        {
            Rows = rows,
            Partners = LookupData.PartnerNames,
            PartnerEmails = partnerEmails
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssignment(string branch, string partner)
    {
        if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(partner))
        {
            return BadRequest(new { error = "Branch and partner are required." });
        }
        if (!LookupData.Branches.Contains(branch))
        {
            return BadRequest(new { error = "Unknown branch." });
        }
        if (!LookupData.PartnerNames.Contains(partner))
        {
            return BadRequest(new { error = "Unknown partner." });
        }

        var assignment = await _db.BranchAssignments.FirstOrDefaultAsync(a => a.BranchName == branch);
        if (assignment == null)
        {
            assignment = new BranchPartnerAssignment { BranchName = branch, AssignedPartner = partner };
            _db.BranchAssignments.Add(assignment);
        }
        else
        {
            assignment.AssignedPartner = partner;
        }

        await _db.SaveChangesAsync();

        var email = LookupData.Partners.FirstOrDefault(p => p.Name == partner)?.Email ?? string.Empty;
        return Json(new { branch, partner, email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetDefaults()
    {
        var defaults = LookupData.DefaultAssignments();
        var existing = await _db.BranchAssignments.ToDictionaryAsync(a => a.BranchName);

        foreach (var def in defaults)
        {
            if (existing.TryGetValue(def.BranchName, out var current))
            {
                current.AssignedPartner = def.AssignedPartner;
            }
            else
            {
                _db.BranchAssignments.Add(new BranchPartnerAssignment
                {
                    BranchName = def.BranchName,
                    AssignedPartner = def.AssignedPartner
                });
            }
        }

        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = "Branch assignments reset to defaults.";
        return RedirectToAction(nameof(Index));
    }
}
