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
        var partners = await _db.Partners.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var partnerEmails = partners.ToDictionary(p => p.Name, p => p.Email);
        var partnerNames = partners.Select(p => p.Name).ToList();
        var branches = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).Select(b => b.Name).ToListAsync();
        var assignments = await _db.BranchAssignments.AsNoTracking().ToDictionaryAsync(a => a.BranchName, a => a.AssignedPartner);

        var assignmentRows = branches
            .Select(b =>
            {
                var partner = assignments.TryGetValue(b, out var p)
                    ? p
                    : (partnerNames.FirstOrDefault() ?? string.Empty);
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
            Partners = partners.Select(p => new PartnerRow { Name = p.Name, FullName = p.FullName, Email = p.Email }).ToList(),
            Branches = branches,
            Assignments = assignmentRows,
            PartnerEmails = partnerEmails
        };

        return View(vm);
    }

    // ---------- SQ staff ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPartner(string name, string fullName, string email)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            TempData["StatusMessage"] = "Staff key cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Partners.AnyAsync(p => p.Name == trimmed))
        {
            TempData["StatusMessage"] = $"Staff member \"{trimmed}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.Partners.Add(new BusinessPartner
        {
            Name = trimmed,
            FullName = (fullName ?? string.Empty).Trim(),
            Email = (email ?? string.Empty).Trim()
        });
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = $"Staff member \"{trimmed}\" added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePartner(string name, string newName, string fullName, string email)
    {
        var oldTrim = (name ?? string.Empty).Trim();
        var newTrim = (newName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(oldTrim) || string.IsNullOrWhiteSpace(newTrim))
        {
            TempData["StatusMessage"] = "Staff key cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        var partner = await _db.Partners.FirstOrDefaultAsync(p => p.Name == oldTrim);
        if (partner == null)
        {
            TempData["StatusMessage"] = $"Staff member \"{oldTrim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        var newFullName = (fullName ?? string.Empty).Trim();
        var newEmail = (email ?? string.Empty).Trim();

        if (oldTrim == newTrim)
        {
            partner.FullName = newFullName;
            partner.Email = newEmail;
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"\"{oldTrim}\" updated.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Partners.AnyAsync(p => p.Name == newTrim))
        {
            TempData["StatusMessage"] = $"Staff member \"{newTrim}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        // Name is the primary key, so a rename is a remove + insert, then a cascade
        // over everything that stores the key as text: FeedbackCase.BusinessPartner
        // and BranchPartnerAssignment.AssignedPartner.
        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Partners.Remove(partner);
        await _db.SaveChangesAsync();

        _db.Partners.Add(new BusinessPartner
        {
            Name = newTrim,
            FullName = newFullName,
            Email = newEmail
        });
        await _db.SaveChangesAsync();

        var cases = await _db.Cases.Where(c => c.BusinessPartner == oldTrim).ToListAsync();
        foreach (var c in cases) c.BusinessPartner = newTrim;

        var assignments = await _db.BranchAssignments.Where(a => a.AssignedPartner == oldTrim).ToListAsync();
        foreach (var a in assignments) a.AssignedPartner = newTrim;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["StatusMessage"] =
            $"Renamed \"{oldTrim}\" to \"{newTrim}\" ({cases.Count} case{(cases.Count == 1 ? "" : "s")}, " +
            $"{assignments.Count} branch{(assignments.Count == 1 ? "" : "es")} updated).";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Branches ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBranch(string name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            TempData["StatusMessage"] = "Branch name cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Branches.AnyAsync(b => b.Name == trimmed))
        {
            TempData["StatusMessage"] = $"Branch \"{trimmed}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.Branches.Add(new Branch { Name = trimmed });

        var firstPartner = await _db.Partners.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).FirstOrDefaultAsync();
        if (!string.IsNullOrEmpty(firstPartner))
        {
            _db.BranchAssignments.Add(new BranchPartnerAssignment
            {
                BranchName = trimmed,
                AssignedPartner = firstPartner
            });
        }

        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Branch \"{trimmed}\" added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameBranch(string oldName, string newName)
    {
        var oldTrim = (oldName ?? string.Empty).Trim();
        var newTrim = (newName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(oldTrim) || string.IsNullOrWhiteSpace(newTrim))
        {
            TempData["StatusMessage"] = "Branch names cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        if (oldTrim == newTrim)
        {
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Branches.AnyAsync(b => b.Name == newTrim))
        {
            TempData["StatusMessage"] = $"Branch \"{newTrim}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await _db.Branches.FirstOrDefaultAsync(b => b.Name == oldTrim);
        if (existing == null)
        {
            TempData["StatusMessage"] = $"Branch \"{oldTrim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        // Branch.Name is the primary key; remove the old row and insert the new one,
        // then cascade-update referencing tables (FeedbackCase.Branch, BranchPartnerAssignment.BranchName).
        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Branches.Remove(existing);
        await _db.SaveChangesAsync();
        _db.Branches.Add(new Branch { Name = newTrim });
        await _db.SaveChangesAsync();

        var cases = await _db.Cases.Where(c => c.Branch == oldTrim).ToListAsync();
        foreach (var c in cases) c.Branch = newTrim;

        var assignment = await _db.BranchAssignments.FirstOrDefaultAsync(a => a.BranchName == oldTrim);
        if (assignment != null)
        {
            var partner = assignment.AssignedPartner;
            _db.BranchAssignments.Remove(assignment);
            await _db.SaveChangesAsync();
            _db.BranchAssignments.Add(new BranchPartnerAssignment
            {
                BranchName = newTrim,
                AssignedPartner = partner
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["StatusMessage"] = $"Renamed \"{oldTrim}\" to \"{newTrim}\" ({cases.Count} case{(cases.Count == 1 ? "" : "s")} updated).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteBranch(string name)
    {
        var trim = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trim))
        {
            TempData["StatusMessage"] = "Branch name required.";
            return RedirectToAction(nameof(Index));
        }

        var caseCount = await _db.Cases.CountAsync(c => c.Branch == trim);
        if (caseCount > 0)
        {
            TempData["StatusMessage"] = $"Cannot delete \"{trim}\": {caseCount} case{(caseCount == 1 ? "" : "s")} reference it. Reassign or delete those cases first.";
            return RedirectToAction(nameof(Index));
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Name == trim);
        if (branch == null)
        {
            TempData["StatusMessage"] = $"Branch \"{trim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        var assignment = await _db.BranchAssignments.FirstOrDefaultAsync(a => a.BranchName == trim);
        if (assignment != null) _db.BranchAssignments.Remove(assignment);
        _db.Branches.Remove(branch);
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = $"Branch \"{trim}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Assignments (unchanged behavior) ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAssignment(string branch, string partner)
    {
        if (string.IsNullOrWhiteSpace(branch) || string.IsNullOrWhiteSpace(partner))
        {
            return BadRequest(new { error = "Branch and partner are required." });
        }
        if (!await _db.Branches.AnyAsync(b => b.Name == branch))
        {
            return BadRequest(new { error = "Unknown branch." });
        }
        if (!await _db.Partners.AnyAsync(p => p.Name == partner))
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

        var email = (await _db.Partners.AsNoTracking().FirstOrDefaultAsync(p => p.Name == partner))?.Email ?? string.Empty;
        return Json(new { branch, partner, email });
    }
}
