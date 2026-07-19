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
        builder.Property(b => b.Currency).HasMaxLength(3).HasDefaultValue("YER");
        builder.Property(b => b.ExchangeRateToYer).HasPrecision(18, 6).HasDefaultValue(1m);
        builder.Property(b => b.ExchangeRateSource).HasMaxLength(30).HasDefaultValue("same_currency");
        builder.Property(b => b.BillNumber).HasMaxLength(50);
        builder.Property(b => b.Description).HasMaxLength(500);
        builder.Property(b => b.IsOpeningBalance).HasDefaultValue(false);

        // Enum stored as string in DB for readability
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(BillStatus.Unpaid);

        builder.HasIndex(b => b.BranchId);
        builder.HasIndex(b => b.SupplierId);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => new { b.SupplierId, b.IsOpeningBalance });
        builder.HasIndex(b => new { b.SupplierId, b.Currency });

        // Relationships
        builder.HasOne(b => b.Supplier)
            .WithMany(s => s.Bills)
            .HasForeignKey(b => b.SupplierId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
