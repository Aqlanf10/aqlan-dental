using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// Lab Sprint 3 — Configuration for LabWorkPrice entity.
/// Unique index on (LabId, WorkTypeId) ensures one price per lab per work type.
/// </summary>
public class LabWorkPriceConfiguration : IEntityTypeConfiguration<LabWorkPrice>
{
    public void Configure(EntityTypeBuilder<LabWorkPrice> builder)
    {
        builder.Property(p => p.UrgentSurchargeType).HasMaxLength(20);
        builder.Property(p => p.Notes).HasMaxLength(1000);

        // Unique index: one price per lab per work type
        builder.HasIndex(p => new { p.LabId, p.WorkTypeId }).IsUnique();

        builder.HasOne(p => p.Lab)
            .WithMany()
            .HasForeignKey(p => p.LabId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.WorkType)
            .WithMany()
            .HasForeignKey(p => p.WorkTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
