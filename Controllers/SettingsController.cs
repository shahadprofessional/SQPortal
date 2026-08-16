using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SQPortal.Data;
using SQPortal.Helpers;
using SQPortal.Models.Entities;
using SQPortal.Models.ViewModels.Settings;
using SQPortal.Services;

namespace SQPortal.Controllers;

public class SettingsController : Controller
{
    private readonly SQPortalDbContext _db;
    private readonly ManagerAssignmentService _managerAssignments;
    private readonly AuditService _audit;

    public SettingsController(SQPortalDbContext db, ManagerAssignmentService managerAssignments, AuditService audit)
    {
        _db = db;
        _managerAssignments = managerAssignments;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        await _managerAssignments.EnsureEveryBranchHasManagerAsync();

        var partners = await _db.Partners.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        var managers = await _db.Managers.AsNoTracking().OrderBy(m => m.Name).ToListAsync();
        var branchNames = await _db.Branches.AsNoTracking().OrderBy(b => b.Name).Select(b => b.Name).ToListAsync();

        var partnerOf = await _db.BranchAssignments.AsNoTracking()
            .ToDictionaryAsync(a => a.BranchName, a => a.AssignedPartner);
        var managerOf = await _db.ManagerAssignments.AsNoTracking()
            .ToDictionaryAsync(a => a.BranchName, a => a.AssignedManager);

        var branchRows = branchNames
            .Select(b => new BranchRow
            {
                Name = b,
                Manager = managerOf.TryGetValue(b, out var m) ? m : string.Empty,
                Partner = partnerOf.TryGetValue(b, out var p) ? p : string.Empty
            })
            .ToList();

        var vm = new SettingsViewModel
        {
            Branches = branchRows,
            Managers = managers.Select(m => new PartnerRow { Name = m.Name, FullName = m.FullName, Email = m.Email }).ToList(),
            Partners = partners.Select(p => new PartnerRow { Name = p.Name, FullName = p.FullName, Email = p.Email }).ToList()
        };

        return View(vm);
    }

    // ---------- Branch managers ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddManager(string name, string fullName, string email)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            TempData["StatusMessage"] = "Manager key cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        var newFullName = (fullName ?? string.Empty).Trim();
        var newEmail = (email ?? string.Empty).Trim();
        if (!ValidatePersonFields(trimmed, newFullName, newEmail, out var problem))
        {
            TempData["StatusMessage"] = problem;
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Managers.AnyAsync(m => m.Name == trimmed))
        {
            TempData["StatusMessage"] = $"Manager \"{trimmed}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.Managers.Add(new BranchManager
        {
            Name = trimmed,
            FullName = newFullName,
            Email = newEmail
        });
        await _db.SaveChangesAsync();

        var assigned = await _managerAssignments.EnsureEveryBranchHasManagerAsync();
        var note = assigned == 0
            ? string.Empty
            : $" {assigned} branch{(assigned == 1 ? "" : "es")} assigned to them.";

        await _audit.LogAsync("Settings", $"Manager \"{trimmed}\" added");
        TempData["StatusMessage"] = $"Manager \"{trimmed}\" added.{note}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateManager(string name, string newName, string fullName, string email)
    {
        var oldTrim = (name ?? string.Empty).Trim();
        var newTrim = (newName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(oldTrim) || string.IsNullOrWhiteSpace(newTrim))
        {
            TempData["StatusMessage"] = "Manager key cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        var manager = await _db.Managers.FirstOrDefaultAsync(m => m.Name == oldTrim);
        if (manager == null)
        {
            TempData["StatusMessage"] = $"Manager \"{oldTrim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        var newFullName = (fullName ?? string.Empty).Trim();
        var newEmail = (email ?? string.Empty).Trim();
        if (!ValidatePersonFields(newTrim, newFullName, newEmail, out var problem))
        {
            TempData["StatusMessage"] = problem;
            return RedirectToAction(nameof(Index));
        }

        if (oldTrim == newTrim)
        {
            manager.FullName = newFullName;
            manager.Email = newEmail;
            _audit.Log("Settings", $"Manager \"{oldTrim}\" updated");
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"\"{oldTrim}\" updated.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Managers.AnyAsync(m => m.Name == newTrim))
        {
            TempData["StatusMessage"] = $"Manager \"{newTrim}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        // Name is the primary key, so a rename is a remove + insert;
        // assignments follow the new name.
        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Managers.Remove(manager);
        await _db.SaveChangesAsync();

        _db.Managers.Add(new BranchManager
        {
            Name = newTrim,
            FullName = newFullName,
            Email = newEmail
        });
        await _db.SaveChangesAsync();

        var assignments = await _db.ManagerAssignments.Where(a => a.AssignedManager == oldTrim).ToListAsync();
        foreach (var a in assignments) a.AssignedManager = newTrim;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        await _audit.LogAsync("Settings", $"Manager renamed \"{oldTrim}\" to \"{newTrim}\"");
        TempData["StatusMessage"] =
            $"Renamed \"{oldTrim}\" to \"{newTrim}\" — " +
            $"{assignments.Count} branch{(assignments.Count == 1 ? "" : "es")} now run by them.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteManager(string name)
    {
        var trim = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trim))
        {
            TempData["StatusMessage"] = "Manager key required.";
            return RedirectToAction(nameof(Index));
        }

        var manager = await _db.Managers.FirstOrDefaultAsync(m => m.Name == trim);
        if (manager == null)
        {
            TempData["StatusMessage"] = $"Manager \"{trim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        // Reassign this manager's branches to the remaining roster; with no
        // successor the assignment rows are removed.
        var assignments = await _db.ManagerAssignments.Where(a => a.AssignedManager == trim).ToListAsync();
        var successor = await _db.Managers.AsNoTracking()
            .Where(m => m.Name != trim)
            .OrderBy(m => m.Name)
            .Select(m => m.Name)
            .FirstOrDefaultAsync();

        if (string.IsNullOrEmpty(successor))
        {
            _db.ManagerAssignments.RemoveRange(assignments);
        }
        else
        {
            foreach (var a in assignments) a.AssignedManager = successor;
        }

        _db.Managers.Remove(manager);
        _audit.Log("Settings", $"Manager \"{trim}\" removed");
        await _db.SaveChangesAsync();

        var handover = assignments.Count == 0
            ? string.Empty
            : string.IsNullOrEmpty(successor)
                ? $" {assignments.Count} branch{(assignments.Count == 1 ? " is" : "es are")} now without a manager — add one to reassign."
                : $" {assignments.Count} branch{(assignments.Count == 1 ? "" : "es")} handed to \"{successor}\".";

        TempData["StatusMessage"] = $"Removed \"{trim}\".{handover}";
        return RedirectToAction(nameof(Index));
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

        var newFullName = (fullName ?? string.Empty).Trim();
        var newEmail = (email ?? string.Empty).Trim();
        if (!ValidatePersonFields(trimmed, newFullName, newEmail, out var problem))
        {
            TempData["StatusMessage"] = problem;
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
            FullName = newFullName,
            Email = newEmail
        });
        _audit.Log("Settings", $"Staff member \"{trimmed}\" added");
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
        if (!ValidatePersonFields(newTrim, newFullName, newEmail, out var problem))
        {
            TempData["StatusMessage"] = problem;
            return RedirectToAction(nameof(Index));
        }

        if (oldTrim == newTrim)
        {
            partner.FullName = newFullName;
            partner.Email = newEmail;
            _audit.Log("Settings", $"Staff member \"{oldTrim}\" updated");
            await _db.SaveChangesAsync();

            TempData["StatusMessage"] = $"\"{oldTrim}\" updated.";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Partners.AnyAsync(p => p.Name == newTrim))
        {
            TempData["StatusMessage"] = $"Staff member \"{newTrim}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        // Name is the primary key, so a rename is a remove + insert. Branch
        // assignments follow the new name; past cases keep the name of the
        // person who actually handled them.
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

        var assignments = await _db.BranchAssignments.Where(a => a.AssignedPartner == oldTrim).ToListAsync();
        foreach (var a in assignments) a.AssignedPartner = newTrim;

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        var keptCases = await _db.Cases.IgnoreQueryFilters().CountAsync(c => c.BusinessPartner == oldTrim);
        var kept = keptCases == 0
            ? string.Empty
            : $" {keptCases} past case{(keptCases == 1 ? "" : "s")} stay with \"{oldTrim}\".";

        await _audit.LogAsync("Settings", $"Staff member renamed \"{oldTrim}\" to \"{newTrim}\"");
        TempData["StatusMessage"] =
            $"Renamed \"{oldTrim}\" to \"{newTrim}\" — " +
            $"{assignments.Count} branch{(assignments.Count == 1 ? "" : "es")} now assigned to them.{kept}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePartner(string name)
    {
        var trim = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trim))
        {
            TempData["StatusMessage"] = "Staff key required.";
            return RedirectToAction(nameof(Index));
        }

        var partner = await _db.Partners.FirstOrDefaultAsync(p => p.Name == trim);
        if (partner == null)
        {
            TempData["StatusMessage"] = $"Staff member \"{trim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        // Past cases keep the handler's name; the branches become unassigned.
        var caseCount = await _db.Cases.IgnoreQueryFilters().CountAsync(c => c.BusinessPartner == trim);

        var assignments = await _db.BranchAssignments.Where(a => a.AssignedPartner == trim).ToListAsync();
        _db.BranchAssignments.RemoveRange(assignments);
        _db.Partners.Remove(partner);
        _audit.Log("Settings", $"Staff member \"{trim}\" removed");
        await _db.SaveChangesAsync();

        var freed = assignments.Count == 0
            ? string.Empty
            : $" {assignments.Count} branch{(assignments.Count == 1 ? "" : "es")} lost their owner — reassign them below.";
        var kept = caseCount == 0
            ? string.Empty
            : $" {caseCount} past case{(caseCount == 1 ? "" : "s")} keep their name.";

        TempData["StatusMessage"] = $"Removed \"{trim}\".{freed}{kept}";
        return RedirectToAction(nameof(Index));
    }

    // ---------- Branches ----------

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddBranch(string name, string? manager, string? partner)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            TempData["StatusMessage"] = "Branch name cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        if (trimmed.Length > 100)
        {
            TempData["StatusMessage"] = "Branch name is too long (max 100 characters).";
            return RedirectToAction(nameof(Index));
        }

        if (await _db.Branches.AnyAsync(b => b.Name == trimmed))
        {
            TempData["StatusMessage"] = $"Branch \"{trimmed}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        _db.Branches.Add(new Branch { Name = trimmed });
        await _db.SaveChangesAsync();

        await SetManagerAssignmentAsync(trimmed, await ResolveManagerAsync(manager));
        await SetPartnerAssignmentAsync(trimmed, await ResolvePartnerAsync(partner));
        _audit.Log("Settings", $"Branch \"{trimmed}\" added");
        await _db.SaveChangesAsync();
        TempData["StatusMessage"] = $"Branch \"{trimmed}\" added.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Saves a branch row: name, manager and SQ owner in one submit.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBranch(string oldName, string newName, string? manager, string? partner)
    {
        var oldTrim = (oldName ?? string.Empty).Trim();
        var newTrim = (newName ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(oldTrim) || string.IsNullOrWhiteSpace(newTrim))
        {
            TempData["StatusMessage"] = "Branch name cannot be empty.";
            return RedirectToAction(nameof(Index));
        }

        if (newTrim.Length > 100)
        {
            TempData["StatusMessage"] = "Branch name is too long (max 100 characters).";
            return RedirectToAction(nameof(Index));
        }

        var existing = await _db.Branches.FirstOrDefaultAsync(b => b.Name == oldTrim);
        if (existing == null)
        {
            TempData["StatusMessage"] = $"Branch \"{oldTrim}\" not found.";
            return RedirectToAction(nameof(Index));
        }

        var renaming = oldTrim != newTrim;
        if (renaming && await _db.Branches.AnyAsync(b => b.Name == newTrim))
        {
            TempData["StatusMessage"] = $"Branch \"{newTrim}\" already exists.";
            return RedirectToAction(nameof(Index));
        }

        var chosenManager = await ResolveManagerAsync(manager);
        var chosenPartner = await ResolvePartnerAsync(partner);
        var caseCount = 0;

        await using var tx = await _db.Database.BeginTransactionAsync();

        if (renaming)
        {
            // Branch.Name is the primary key, so a rename is a remove + insert;
            // cases and both assignment tables store the name and must follow.
            _db.Branches.Remove(existing);
            await _db.SaveChangesAsync();
            _db.Branches.Add(new Branch { Name = newTrim });
            await _db.SaveChangesAsync();

            // Soft-deleted cases follow the rename too, so a later recovery
            // does not resurface a branch name that no longer exists.
            var cases = await _db.Cases.IgnoreQueryFilters().Where(c => c.Branch == oldTrim).ToListAsync();
            foreach (var c in cases) c.Branch = newTrim;
            caseCount = cases.Count(c => !c.IsDeleted);

            var oldPartnerRow = await _db.BranchAssignments.FirstOrDefaultAsync(a => a.BranchName == oldTrim);
            if (oldPartnerRow != null) _db.BranchAssignments.Remove(oldPartnerRow);

            var oldManagerRow = await _db.ManagerAssignments.FirstOrDefaultAsync(a => a.BranchName == oldTrim);
            if (oldManagerRow != null) _db.ManagerAssignments.Remove(oldManagerRow);

            await _db.SaveChangesAsync();
        }

        await SetManagerAssignmentAsync(newTrim, chosenManager);
        await SetPartnerAssignmentAsync(newTrim, chosenPartner);
        _audit.Log("Settings", renaming ? $"Branch renamed \"{oldTrim}\" to \"{newTrim}\"" : $"Branch \"{newTrim}\" saved");
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        TempData["StatusMessage"] = renaming
            ? $"Renamed \"{oldTrim}\" to \"{newTrim}\" ({caseCount} case{(caseCount == 1 ? "" : "s")} updated)."
            : $"\"{newTrim}\" saved.";
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

        // Soft-deleted cases still name the branch and can be restored, so they
        // count as references even though they are hidden everywhere else.
        var liveCount = await _db.Cases.CountAsync(c => c.Branch == trim);
        var totalCount = await _db.Cases.IgnoreQueryFilters().CountAsync(c => c.Branch == trim);

        if (totalCount > 0)
        {
            TempData["StatusMessage"] = liveCount > 0
                ? $"Cannot delete \"{trim}\": {liveCount} case{(liveCount == 1 ? "" : "s")} reference it. Reassign or delete those cases first."
                : $"Cannot delete \"{trim}\": {totalCount} deleted case{(totalCount == 1 ? "" : "s")} still reference it.";
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

        var managerAssignment = await _db.ManagerAssignments.FirstOrDefaultAsync(a => a.BranchName == trim);
        if (managerAssignment != null) _db.ManagerAssignments.Remove(managerAssignment);

        _db.Branches.Remove(branch);
        _audit.Log("Settings", $"Branch \"{trim}\" deleted");
        await _db.SaveChangesAsync();

        TempData["StatusMessage"] = $"Branch \"{trim}\" deleted.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Server-side field validation; browser maxlength/type=email attributes
    /// are advisory only.
    /// </summary>
    private static bool ValidatePersonFields(string key, string fullName, string email, out string problem)
    {
        if (key.Length > 40)
        {
            problem = "Key is too long (max 40 characters).";
            return false;
        }
        if (fullName.Length > 200)
        {
            problem = "Full name is too long (max 200 characters).";
            return false;
        }
        if (email.Length > 200)
        {
            problem = "Email is too long (max 200 characters).";
            return false;
        }
        if (email.Length > 0 && !DisplayHelpers.IsValidEmailAddress(email))
        {
            problem = "That email address is not valid.";
            return false;
        }

        problem = string.Empty;
        return true;
    }

    /// <summary>The named manager if they exist, otherwise the first on the roster.</summary>
    private async Task<string> ResolveManagerAsync(string? candidate)
    {
        var trim = (candidate ?? string.Empty).Trim();
        if (trim.Length > 0 && await _db.Managers.AnyAsync(m => m.Name == trim)) return trim;

        return await _db.Managers.AsNoTracking().OrderBy(m => m.Name).Select(m => m.Name).FirstOrDefaultAsync()
            ?? string.Empty;
    }

    /// <summary>The named SQ staff member if they exist, otherwise the first on file.</summary>
    private async Task<string> ResolvePartnerAsync(string? candidate)
    {
        var trim = (candidate ?? string.Empty).Trim();
        if (trim.Length > 0 && await _db.Partners.AnyAsync(p => p.Name == trim)) return trim;

        return await _db.Partners.AsNoTracking().OrderBy(p => p.Name).Select(p => p.Name).FirstOrDefaultAsync()
            ?? string.Empty;
    }

    /// <summary>Upserts the manager assignment, or drops it when there is nobody to assign.</summary>
    private async Task SetManagerAssignmentAsync(string branch, string manager)
    {
        var row = await _db.ManagerAssignments.FirstOrDefaultAsync(a => a.BranchName == branch);

        if (string.IsNullOrEmpty(manager))
        {
            if (row != null) _db.ManagerAssignments.Remove(row);
            return;
        }

        if (row == null)
        {
            _db.ManagerAssignments.Add(new BranchManagerAssignment { BranchName = branch, AssignedManager = manager });
        }
        else
        {
            row.AssignedManager = manager;
        }
    }

    /// <summary>Upserts the SQ-staff assignment, or drops it when there is nobody to assign.</summary>
    private async Task SetPartnerAssignmentAsync(string branch, string partner)
    {
        var row = await _db.BranchAssignments.FirstOrDefaultAsync(a => a.BranchName == branch);

        if (string.IsNullOrEmpty(partner))
        {
            if (row != null) _db.BranchAssignments.Remove(row);
            return;
        }

        if (row == null)
        {
            _db.BranchAssignments.Add(new BranchPartnerAssignment { BranchName = branch, AssignedPartner = partner });
        }
        else
        {
            row.AssignedPartner = partner;
        }
    }
}
