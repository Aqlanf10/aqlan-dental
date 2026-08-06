using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class DoctorCommissionPaymentAllocationConfiguration
    : IEntityTypeConfiguration<DoctorCommissionPaymentAllocation>
{
    public void Configure(EntityTypeBuilder<DoctorCommissionPaymentAllocation> builder)
    {
        builder.Property(allocation => allocation.Amount).HasPrecision(18, 2);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_DoctorCommissionPaymentAllocations_PositiveAmount",
                "\"Amount\" > 0");
            table.HasCheckConstraint(
                "CK_DoctorCommissionPaymentAllocations_ExactlyOneTarget",
                "(\"InvoiceLineItemId\" IS NOT NULL) <> (\"CommissionAdjustmentId\" IS NOT NULL)");
        });

        builder.HasOne(allocation => allocation.Payment)
            .WithMany(payment => payment.Allocations)
            .HasForeignKey(allocation => allocation.PaymentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(allocation => allocation.InvoiceLineItem)
            .WithMany()
            .HasForeignKey(allocation => allocation.InvoiceLineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(allocation => allocation.CommissionAdjustment)
            .WithMany()
            .HasForeignKey(allocation => allocation.CommissionAdjustmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(allocation => new { allocation.PaymentId, allocation.InvoiceLineItemId })
            .IsUnique()
            .HasFilter("\"InvoiceLineItemId\" IS NOT NULL");
        builder.HasIndex(allocation => new { allocation.PaymentId, allocation.CommissionAdjustmentId })
            .IsUnique()
            .HasFilter("\"CommissionAdjustmentId\" IS NOT NULL");
        builder.HasIndex(allocation => allocation.InvoiceLineItemId);
        builder.HasIndex(allocation => allocation.CommissionAdjustmentId);
    }
}
