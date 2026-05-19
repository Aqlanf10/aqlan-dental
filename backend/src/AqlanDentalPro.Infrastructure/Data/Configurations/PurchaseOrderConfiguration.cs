using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Fluent API configuration for PurchaseOrder entity indexes and constraints.
/// </summary>
public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.HasKey(po => po.Id);

        builder.ToTable("PurchaseOrders");

        // String properties
        builder.Property(po => po.OrderNumber).HasMaxLength(50).IsRequired();
        builder.Property(po => po.Notes).IsRequired(false);

        // Enum conversion
        builder.Property(po => po.Status).HasConversion<string>().HasMaxLength(30);

        // Decimal precision
        builder.Property(po => po.Subtotal).HasPrecision(12, 2);
        builder.Property(po => po.TaxAmount).HasPrecision(12, 2);
        builder.Property(po => po.TotalAmount).HasPrecision(12, 2);

        // Nullable FK fields
        builder.Property(po => po.ExpectedDate).IsRequired(false);
        builder.Property(po => po.ReceivedDate).IsRequired(false);
        builder.Property(po => po.BranchId).IsRequired(false);
        builder.Property(po => po.CreatedBy).IsRequired(false);

        // Indexes
        builder.HasIndex(po => po.SupplierId);
        builder.HasIndex(po => po.Status);
        builder.HasIndex(po => po.OrderNumber).IsUnique();
        builder.HasIndex(po => po.BranchId);
        builder.HasIndex(po => po.OrderDate);

        // Relationships
        builder.HasOne(po => po.Supplier)
            .WithMany(s => s.PurchaseOrders)
            .HasForeignKey(po => po.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(po => po.Branch)
            .WithMany()
            .HasForeignKey(po => po.BranchId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(po => po.LineItems)
            .WithOne(li => li.PurchaseOrder)
            .HasForeignKey(li => li.PurchaseOrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
