using AqlanDentalPro.Domain.Entities;
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

        builder.HasIndex(b => b.BranchId);
        builder.HasIndex(b => b.SupplierId);
        builder.HasIndex(b => b.Status);
    }
}
