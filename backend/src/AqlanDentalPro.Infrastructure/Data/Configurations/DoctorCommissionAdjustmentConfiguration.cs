using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// CORE-FIN-LAB-ADJ. Deliberately declares its relationships WITHOUT navigation properties:
/// the database still gets real foreign keys, but no query can accidentally <c>Include</c> a
/// required navigation — which the EF Core InMemory provider answers by dropping the entire
/// row rather than just the navigation, a trap this repository has already been bitten by.
/// </summary>
public class DoctorCommissionAdjustmentConfiguration : IEntityTypeConfiguration<DoctorCommissionAdjustment>
{
    public void Configure(EntityTypeBuilder<DoctorCommissionAdjustment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.ToTable("DoctorCommissionAdjustments");

        builder.Property(a => a.PaidCommissionAmount).HasPrecision(12, 2);
        builder.Property(a => a.RecalculatedCommissionAmount).HasPrecision(12, 2);
        builder.Property(a => a.AdjustmentAmount).HasPrecision(12, 2);
        builder.Property(a => a.PreviousLabCost).HasPrecision(12, 2);
        builder.Property(a => a.CurrentLabCost).HasPrecision(12, 2);

        builder.Property(a => a.Reason).IsRequired().HasMaxLength(500);
        builder.Property(a => a.CancelReason).HasMaxLength(500);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(CommissionAdjustmentStatus.Pending);

        // The settlement screen reads "what is still open for this doctor", and the
        // idempotency check reads "everything already raised against this line item".
        builder.HasIndex(a => new { a.DoctorId, a.Status });
        builder.HasIndex(a => a.InvoiceLineItemId);
        builder.HasIndex(a => a.LabOrderId);
        builder.HasIndex(a => a.SettledByPaymentId);

        builder.HasOne<Doctor>()
            .WithMany()
            .HasForeignKey(a => a.DoctorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<InvoiceLineItem>()
            .WithMany()
            .HasForeignKey(a => a.InvoiceLineItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<LabOrder>()
            .WithMany()
            .HasForeignKey(a => a.LabOrderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
