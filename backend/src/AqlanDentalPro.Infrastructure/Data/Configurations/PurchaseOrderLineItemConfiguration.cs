using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for PurchaseOrderLineItem entity indexes and constraints.
/// </summary>
public class PurchaseOrderLineItemConfiguration : IEntityTypeConfiguration<PurchaseOrderLineItem>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLineItem> builder)
    {
        builder.HasKey(li => li.Id);

        builder.ToTable("PurchaseOrderLineItems");

        // String properties
        builder.Property(li => li.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(li => li.ItemDescription).HasMaxLength(500).IsRequired(false);

        // Decimal precision
        builder.Property(li => li.UnitCost).HasPrecision(12, 2);
        builder.Property(li => li.TotalPrice).HasPrecision(12, 2);

        // Nullable FK fields
        builder.Property(li => li.InventoryItemId).IsRequired(false);

        // Indexes
        builder.HasIndex(li => li.PurchaseOrderId);
        builder.HasIndex(li => li.InventoryItemId);

        // Relationships
        builder.HasOne(li => li.InventoryItem)
            .WithMany()
            .HasForeignKey(li => li.InventoryItemId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
