using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SQPortal.Models.Entities;

namespace SQPortal.Data;

public class SQPortalDbContext : DbContext
{
    public SQPortalDbContext(DbContextOptions<SQPortalDbContext> options) : base(options) { }

    public DbSet<FeedbackCase> Cases => Set<FeedbackCase>();
    public DbSet<BusinessPartner> Partners => Set<BusinessPartner>();
    public DbSet<BranchPartnerAssignment> BranchAssignments => Set<BranchPartnerAssignment>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchManager> Managers => Set<BranchManager>();
    public DbSet<BranchManagerAssignment> ManagerAssignments => Set<BranchManagerAssignment>();
    public DbSet<AuditEntry> Audits => Set<AuditEntry>();
    public DbSet<SchemaVersion> SchemaVersions => Set<SchemaVersion>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var rootCausesConverter = new ValueConverter<List<string>, string>(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrEmpty(v)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());

        var rootCausesComparer = new ValueComparer<List<string>>(
            (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
            c => c.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<FeedbackCase>()
            .Property(c => c.RootCauses)
            .HasConversion(rootCausesConverter)
            .Metadata.SetValueComparer(rootCausesComparer);

        // Soft delete: filtered out of every query unless IgnoreQueryFilters is used.
        modelBuilder.Entity<FeedbackCase>()
            .HasQueryFilter(c => !c.IsDeleted);

        // Unique across live and soft-deleted rows so numbers are never reused;
        // 0 is excluded because legacy rows hold it until the startup backfill.
        modelBuilder.Entity<FeedbackCase>()
            .HasIndex(c => c.CaseNumber)
            .IsUnique()
            .HasFilter("[CaseNumber] > 0");

        modelBuilder.Entity<FeedbackCase>()
            .HasIndex(c => c.Date);

        modelBuilder.Entity<FeedbackCase>()
            .HasIndex(c => c.Branch);

        modelBuilder.Entity<AuditEntry>()
            .HasIndex(a => a.TimestampUtc);

        modelBuilder.Entity<AuditEntry>()
            .HasIndex(a => a.CaseId);
    }

    /// <summary>
    /// Numbers cases created before CaseNumber existed: oldest case date first,
    /// ties broken by Id (a unix-ms timestamp, so creation order).
    /// </summary>
    public void BackfillCaseNumbers()
    {
        // Soft-deleted rows are included: they keep their numbers, and the
        // running maximum must account for them.
        var unnumbered = Cases.IgnoreQueryFilters()
            .Where(c => c.CaseNumber == 0)
            .OrderBy(c => c.Date)
            .ThenBy(c => c.Id)
            .ToList();
        if (unnumbered.Count == 0) return;

        var next = NextCaseNumber();
        foreach (var c in unnumbered)
        {
            c.CaseNumber = next++;
        }

        SaveChanges();
    }

    /// <summary>
    /// Next case number; 1 for the first case. Includes soft-deleted rows so a
    /// deleted case's number is never handed out again.
    /// </summary>
    public int NextCaseNumber() =>
        (Cases.IgnoreQueryFilters().Max(c => (int?)c.CaseNumber) ?? 0) + 1;

    public void SeedLookups()
    {
        if (!Partners.Any())
        {
            Partners.AddRange(LookupData.Partners);
        }

        if (!Branches.Any())
        {
            Branches.AddRange(LookupData.Branches.Select(b => new Branch { Name = b }));
        }

        if (!BranchAssignments.Any())
        {
            BranchAssignments.AddRange(LookupData.DefaultAssignments());
        }

        SaveChanges();
    }
}
