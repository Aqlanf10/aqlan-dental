using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M5 FIX: FluentAPI configuration for GeneralTreatmentPlanItem entity.
/// Decimal precision, FK relationships, indexes.
/// </summary>
public class GeneralTreatmentPlanItemConfiguration : IEntityTypeConfiguration<GeneralTreatmentPlanItem>
{
    public void Configure(EntityTypeBuilder<GeneralTreatmentPlanItem> builder)
    {
        builder.Property(i => i.Treatment).HasMaxLength(200).IsRequired();
        builder.Property(i => i.ToothNumber).HasMaxLength(20);
        builder.Property(i => i.Priority).HasMaxLength(20).IsRequired();
        builder.Property(i => i.Status).HasMaxLength(20).IsRequired();
        builder.Property(i => i.EstimatedCost).HasPrecision(12, 2);
        builder.Property(i => i.Notes).HasMaxLength(1000);

        // Performance indexes
        builder.HasIndex(i => i.PatientId);
        builder.HasIndex(i => i.Status);

        // Relationships
        builder.HasOne(i => i.Patient)
            .WithMany()
            .HasForeignKey(i => i.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Doctor)
            .WithMany()
            .HasForeignKey(i => i.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
