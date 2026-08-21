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
        builder.Property(a => a.LabOrderId).IsRequired(false);

        // Indexes
        builder.HasIndex(a => a.InventoryItemId);
        builder.HasIndex(a => a.AdjustmentType);
        builder.HasIndex(a => a.CreatedAt);

        // LABINV-REQ-011. Filtered: only a small minority of adjustments belong to a lab
        // order, and the only query that uses this column asks for one order's rows.
        builder.HasIndex(a => a.LabOrderId)
            .HasFilter("\"LabOrderId\" IS NOT NULL");

        // Relationships
        builder.HasOne(a => a.InventoryItem)
            .WithMany(i => i.Adjustments)
            .HasForeignKey(a => a.InventoryItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
