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

        builder.Property(b => b.TotalAmount).HasPrecision(12, 2);
        builder.Property(b => b.PaidAmount).HasPrecision(12, 2);
        builder.Property(b => b.BillNumber).HasMaxLength(50);
        builder.Property(b => b.Description).HasMaxLength(500);

        // Enum stored as string in DB for readability
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(BillStatus.Unpaid);

        builder.HasIndex(b => b.BranchId);
        builder.HasIndex(b => b.SupplierId);
        builder.HasIndex(b => b.Status);

        // Relationships
        builder.HasOne(b => b.Supplier)
            .WithMany(s => s.Bills)
            .HasForeignKey(b => b.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
