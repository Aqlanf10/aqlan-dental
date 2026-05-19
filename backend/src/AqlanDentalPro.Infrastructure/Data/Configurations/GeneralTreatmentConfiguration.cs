using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M1/M5 FIX: FluentAPI configuration for GeneralTreatment entity.
/// Decimal precision for Cost, FK relationships, indexes.
/// </summary>
public class GeneralTreatmentConfiguration : IEntityTypeConfiguration<GeneralTreatment>
{
    public void Configure(EntityTypeBuilder<GeneralTreatment> builder)
    {
        builder.Property(t => t.TreatmentType).HasMaxLength(200).IsRequired();
        builder.Property(t => t.ToothNumber).HasMaxLength(20);
        builder.Property(t => t.MaterialUsed).HasMaxLength(300);
        builder.Property(t => t.AnesthesiaType).HasMaxLength(100);
        builder.Property(t => t.Cost).HasPrecision(12, 2);
        builder.Property(t => t.Notes).HasMaxLength(1000);

        // Performance indexes
        builder.HasIndex(t => t.PatientId);
        builder.HasIndex(t => t.VisitId);
        builder.HasIndex(t => t.DoctorId);

        // Relationships
        builder.HasOne(t => t.Patient)
            .WithMany()
            .HasForeignKey(t => t.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Visit)
            .WithMany()
            .HasForeignKey(t => t.VisitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(t => t.Doctor)
            .WithMany()
            .HasForeignKey(t => t.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
