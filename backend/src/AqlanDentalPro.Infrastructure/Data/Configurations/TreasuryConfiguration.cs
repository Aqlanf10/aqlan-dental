using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class TreasuryConfiguration : IEntityTypeConfiguration<Treasury>
{
    public void Configure(EntityTypeBuilder<Treasury> entity)
    {
        // A4: Optimistic concurrency — we rely on application-level checks
        // (comparing Balance before/after update) rather than EF Core row versioning.
        // This avoids PostgreSQL-specific xmin complications and works universally.

        // Performance indexes for treasury lookups
        entity.HasIndex(t => t.BranchId);
        entity.HasIndex(t => new { t.BranchId, t.Type });

        // Unique constraint: only one active treasury per branch/type/name combination.
        // Filtered index (PostgreSQL partial index) ensures soft-deleted (IsActive=false)
        // treasuries don't conflict, allowing the same branch/type/name to be reused after deletion.
        entity.HasIndex(t => new { t.BranchId, t.Type, t.Name })
            .HasFilter("\"IsActive\" = true")
            .IsUnique()
            .HasDatabaseName("IX_Treasuries_BranchId_Type_Name_Unique");
    }
}
