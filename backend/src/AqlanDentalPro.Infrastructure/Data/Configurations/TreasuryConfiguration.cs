using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class TreasuryConfiguration : IEntityTypeConfiguration<Treasury>
{
    public void Configure(EntityTypeBuilder<Treasury> entity)
    {
        // A4: Optimistic concurrency — use PostgreSQL xmin system column for row versioning
        // Instead of a separate byte[] Version column (which doesn't work well with PostgreSQL),
        // we use the built-in xmin column that PostgreSQL maintains automatically.
        entity.UseXminAsConcurrencyToken();

        // Performance indexes for treasury lookups
        entity.HasIndex(t => t.BranchId);
        entity.HasIndex(t => new { t.BranchId, t.Type });
    }
}
