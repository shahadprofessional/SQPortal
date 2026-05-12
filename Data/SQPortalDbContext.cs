using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SQPortal.Models.Entities;

namespace SQPortal.Data;

public class SQPortalDbContext : DbContext
{
    public SQPortalDbContext(DbContextOptions<SQPortalDbContext> options) : base(options) { }

    public DbSet<FeedbackCase> Cases => Set<FeedbackCase>();
    public DbSet<BusinessPartner> Partners => Set<BusinessPartner>();
    public DbSet<BranchPartnerAssignment> BranchAssignments => Set<BranchPartnerAssignment>();

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

        modelBuilder.Entity<FeedbackCase>()
            .HasIndex(c => c.CustomerPhone);

        modelBuilder.Entity<FeedbackCase>()
            .HasIndex(c => c.Date);

        modelBuilder.Entity<FeedbackCase>()
            .HasIndex(c => c.Branch);
    }

    public void SeedLookups()
    {
        if (!Partners.Any())
        {
            Partners.AddRange(LookupData.Partners);
        }

        if (!BranchAssignments.Any())
        {
            BranchAssignments.AddRange(LookupData.DefaultAssignments());
        }

        SaveChanges();
    }
}
