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

        // Decimal precision
        builder.Property(p => p.Amount).HasPrecision(12, 2);
        builder.Property(p => p.PaymentMethod).HasMaxLength(50);
        builder.Property(p => p.ReferenceNumber).HasMaxLength(100);

        // ─── Explicit FK: SupplierBillPayment → SupplierBill (Cascade delete) ───
        // When a bill is hard-deleted, its payments are removed too.
        builder.HasOne(p => p.SupplierBill)
            .WithMany(b => b.Payments)
            .HasForeignKey(p => p.SupplierBillId)
            .OnDelete(DeleteBehavior.Cascade);

        // ─── Explicit FK: SupplierBillPayment → CashFlowTransaction (SetNull) ───
        // If the CashFlowTransaction is deleted, the payment record stays but loses the link.
        builder.HasOne(p => p.CashFlowTransaction)
            .WithMany()
            .HasForeignKey(p => p.CashFlowTransactionId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(p => p.SupplierBillId);
        builder.HasIndex(p => p.PaymentDate);
    }
}
