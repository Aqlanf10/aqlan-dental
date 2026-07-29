using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class LabOrderStatusHistoryConfiguration : IEntityTypeConfiguration<LabOrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<LabOrderStatusHistory> builder)
    {
        builder.Property(h => h.FromStatus).HasMaxLength(50).IsRequired();
        builder.Property(h => h.ToStatus).HasMaxLength(50).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(1000);

        builder.HasIndex(h => h.LabOrderId);

        builder.HasOne(h => h.LabOrder)
            .WithMany(o => o.StatusHistory)
            .HasForeignKey(h => h.LabOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.ChangedByUser)
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
