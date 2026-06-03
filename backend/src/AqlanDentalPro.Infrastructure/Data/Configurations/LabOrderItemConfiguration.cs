using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Lab Sprint 3 — Configuration for LabOrderItem entity.
/// </summary>
public class LabOrderItemConfiguration : IEntityTypeConfiguration<LabOrderItem>
{
    public void Configure(EntityTypeBuilder<LabOrderItem> builder)
    {
        builder.Property(i => i.ToothNumber).HasMaxLength(50);
        builder.Property(i => i.Arch).HasMaxLength(10);
        builder.Property(i => i.Shade).HasMaxLength(50);
        builder.Property(i => i.RestorationType).HasMaxLength(100);
        builder.Property(i => i.Instructions).HasMaxLength(2000);

        builder.HasIndex(i => i.LabOrderId);
        builder.HasIndex(i => i.WorkTypeId);

        builder.HasOne(i => i.LabOrder)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.LabOrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.WorkType)
            .WithMany()
            .HasForeignKey(i => i.WorkTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
