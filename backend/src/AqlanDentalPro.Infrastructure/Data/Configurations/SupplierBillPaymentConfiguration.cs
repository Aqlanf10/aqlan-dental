using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class SupplierBillPaymentConfiguration : IEntityTypeConfiguration<SupplierBillPayment>
{
    public void Configure(EntityTypeBuilder<SupplierBillPayment> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("SupplierBillPayments");

        builder.Property(p => p.Amount).HasPrecision(12, 2);
        builder.Property(p => p.PaymentMethod).HasMaxLength(50);
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);

        builder.HasIndex(p => p.SupplierBillId);
    }
}
