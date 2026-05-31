using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for InventoryAdjustment entity indexes and constraints.
/// </summary>
public class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.HasKey(a => a.Id);

        builder.ToTable("InventoryAdjustments");

        // String properties
        builder.Property(a => a.AdjustmentType).HasMaxLength(30).IsRequired();
        builder.Property(a => a.Reason).HasMaxLength(500).IsRequired(false);

        // Nullable FK fields
        builder.Property(a => a.PurchaseOrderLineItemId).IsRequired(false);
        builder.Property(a => a.AdjustedBy).IsRequired(false);

        // Indexes
        builder.HasIndex(a => a.InventoryItemId);
        builder.HasIndex(a => a.AdjustmentType);
        builder.HasIndex(a => a.CreatedAt);

        // Relationships
        builder.HasOne(a => a.InventoryItem)
            .WithMany(i => i.Adjustments)
            .HasForeignKey(a => a.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
