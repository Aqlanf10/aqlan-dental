using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class TreasuryConfiguration : IEntityTypeConfiguration<Treasury>
{
    public void Configure(EntityTypeBuilder<Treasury> entity)
    {
        // A4: Optimistic concurrency via row version
        entity.Property(t => t.Version).IsRowVersion().IsConcurrencyToken();

        // Performance indexes for treasury lookups
        entity.HasIndex(t => t.BranchId);
        entity.HasIndex(t => new { t.BranchId, t.Type });
    }
}
