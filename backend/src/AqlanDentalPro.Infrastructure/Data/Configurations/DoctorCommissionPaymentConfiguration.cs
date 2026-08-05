using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class DoctorCommissionPaymentConfiguration : IEntityTypeConfiguration<DoctorCommissionPayment>
{
    public void Configure(EntityTypeBuilder<DoctorCommissionPayment> builder)
    {
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_DoctorCommissionPayments_Currency",
            "\"Currency\" IN ('YER', 'SAR', 'USD')"));
        builder.Property(payment => payment.Amount).HasPrecision(18, 2);
        builder.Property(payment => payment.Currency).HasMaxLength(3).IsRequired();

        builder.HasIndex(payment => new
        {
            payment.DoctorId,
            payment.BranchId,
            payment.Currency,
            payment.PaymentDate
        });

        builder.HasOne(payment => payment.Branch)
            .WithMany()
            .HasForeignKey(payment => payment.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
