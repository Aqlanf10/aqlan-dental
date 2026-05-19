using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for Inventory entity indexes, max-length, and relationships.
/// </summary>
public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.HasKey(i => i.Id);

        builder.ToTable("Inventory");

        // String properties
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Category).HasMaxLength(100).IsRequired(false);
        builder.Property(i => i.Unit).HasMaxLength(30).IsRequired(false);
        builder.Property(i => i.BatchNumber).HasMaxLength(50).IsRequired(false);

        // Nullable FK fields
        builder.Property(i => i.CostPerUnit).HasPrecision(12, 2).IsRequired(false);
        builder.Property(i => i.BranchId).IsRequired(false);
        builder.Property(i => i.DefaultSupplierId).IsRequired(false);
        builder.Property(i => i.ExpiryDate).IsRequired(false);

        // Indexes
        builder.HasIndex(i => i.Name);
        builder.HasIndex(i => i.Category);
        builder.HasIndex(i => i.BranchId);
        builder.HasIndex(i => i.DefaultSupplierId);
        builder.HasIndex(i => i.ExpiryDate);

        // Relationships
        builder.HasOne(i => i.Branch)
            .WithMany()
            .HasForeignKey(i => i.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(i => i.DefaultSupplier)
            .WithMany()
            .HasForeignKey(i => i.DefaultSupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        // Note: InventoryAdjustment relationship is configured from the dependent side
        // in InventoryAdjustmentConfiguration to avoid duplicate configuration.
    }
}
