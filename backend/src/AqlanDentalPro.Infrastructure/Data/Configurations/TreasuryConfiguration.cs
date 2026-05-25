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
    }
}
