using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class SupplierBillConfiguration : IEntityTypeConfiguration<SupplierBill>
{
    public void Configure(EntityTypeBuilder<SupplierBill> builder)
    {
        builder.HasKey(b => b.Id);
        builder.ToTable("SupplierBills");

        // Decimal precision
        builder.Property(b => b.TotalAmount).HasPrecision(12, 2);
        builder.Property(b => b.PaidAmount).HasPrecision(12, 2);
        builder.Property(b => b.BillNumber).HasMaxLength(50);
        builder.Property(b => b.Description).HasMaxLength(500);

        // ─── BillStatus: Store as string ('Unpaid','PartiallyPaid','FullyPaid','Cancelled') ───
        // Sprint 2: Consistent with PurchaseOrderStatus enum-to-string mapping.
        // Previously stored as raw int (0,1,2,3) which caused confusion on the frontend.
        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        // ─── Explicit FK: SupplierBill → Supplier (Restrict delete if bills exist) ───
        // Prevents accidental supplier deletion when bills reference them.
        builder.HasOne(b => b.Supplier)
            .WithMany(s => s.Bills)
            .HasForeignKey(b => b.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // ─── Explicit FK: SupplierBill → PurchaseOrder (SetNull if PO is deleted) ───
        builder.HasOne(b => b.PurchaseOrder)
            .WithMany()
            .HasForeignKey(b => b.PurchaseOrderId)
            .OnDelete(DeleteBehavior.SetNull);

        // ─── Explicit FK: SupplierBill → Branch (Restrict) ───
        builder.HasOne(b => b.Branch)
            .WithMany()
            .HasForeignKey(b => b.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(b => b.BranchId);
        builder.HasIndex(b => b.SupplierId);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.BillNumber).IsUnique();
    }
}
