using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M1/M5/M2 FIX: FluentAPI configuration for SurgeryCase entity.
/// SurgeryCaseStatus enum stored as string in DB for backward compatibility.
/// </summary>
public class SurgeryCaseConfiguration : IEntityTypeConfiguration<SurgeryCase>
{
    public void Configure(EntityTypeBuilder<SurgeryCase> builder)
    {
        builder.Property(s => s.CaseNumber).HasMaxLength(30).IsRequired();
        // M2 FIX: Enum conversion
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.SurgeryType).HasMaxLength(200).IsRequired();
        builder.Property(s => s.TeethInvolved).HasMaxLength(200);

        builder.HasIndex(s => s.CaseNumber).IsUnique();
        builder.HasIndex(s => s.PatientId);
        builder.HasIndex(s => s.DoctorId);
        builder.HasIndex(s => s.Status);

        builder.HasOne(s => s.Patient)
            .WithMany()
            .HasForeignKey(s => s.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Doctor)
            .WithMany()
            .HasForeignKey(s => s.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
