using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class AdvancePaymentConfiguration : IEntityTypeConfiguration<AdvancePayment>
{
    public void Configure(EntityTypeBuilder<AdvancePayment> builder)
    {
        builder.Property(a => a.Amount).HasPrecision(12, 2);
        builder.Property(a => a.Reason).HasMaxLength(500);
        builder.Property(a => a.RejectionReason).HasMaxLength(500);

        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
