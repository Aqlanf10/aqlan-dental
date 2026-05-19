using AqlanDentalPro.Domain.Entities;
using AqlanDentalPro.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M1/M5/M2 FIX: FluentAPI configuration for OrthoCase entity.
/// OrthoCaseStatus enum stored as string in DB for backward compatibility.
/// </summary>
public class OrthoCaseConfiguration : IEntityTypeConfiguration<OrthoCase>
{
    public void Configure(EntityTypeBuilder<OrthoCase> builder)
    {
        builder.Property(o => o.CaseNumber).HasMaxLength(30).IsRequired();
        // M2 FIX: Enum conversion
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.TotalFee).HasPrecision(12, 2);
        builder.Property(o => o.ApplianceType).HasMaxLength(100);
        builder.Property(o => o.CurrentStage).HasMaxLength(100);
        builder.Property(o => o.ExtractionDecisionValue).HasMaxLength(50);
        builder.Property(o => o.RetentionPlan).HasMaxLength(500);

        builder.HasIndex(o => o.CaseNumber).IsUnique();
        builder.HasIndex(o => o.PatientId);
        builder.HasIndex(o => o.DoctorId);
        builder.HasIndex(o => o.BranchId);
        builder.HasIndex(o => o.Status);

        builder.HasOne(o => o.Patient)
            .WithMany(p => p.OrthoCases)
            .HasForeignKey(o => o.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.Doctor)
            .WithMany()
            .HasForeignKey(o => o.DoctorId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(o => o.Branch)
            .WithMany()
            .HasForeignKey(o => o.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
