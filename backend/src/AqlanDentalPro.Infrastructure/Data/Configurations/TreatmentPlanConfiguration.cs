using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M5 FIX: FluentAPI configuration for TreatmentPlan entity.
/// Index on OrthoCaseId, string constraints, FK.
/// </summary>
public class TreatmentPlanConfiguration : IEntityTypeConfiguration<TreatmentPlan>
{
    public void Configure(EntityTypeBuilder<TreatmentPlan> builder)
    {
        // Performance indexes
        builder.HasIndex(p => p.OrthoCaseId);

        // Unique composite index: each ortho case can have at most one plan per label (A/B/C)
        builder.HasIndex(p => new { p.OrthoCaseId, p.PlanLabel }).IsUnique();

        // PlanLabel for Plan A/B/C
        builder.Property(p => p.PlanLabel).HasMaxLength(5).HasDefaultValue("A");
        builder.Property(p => p.ApplianceType).HasMaxLength(200);
        builder.Property(p => p.BracketSystem).HasMaxLength(200);
        builder.Property(p => p.InitialWire).HasMaxLength(200);
        builder.Property(p => p.ExtractionPlan).HasMaxLength(1000);
        builder.Property(p => p.AnchoragePlan).HasMaxLength(1000);
        builder.Property(p => p.RetentionPlan).HasMaxLength(1000);
        builder.Property(p => p.TreatmentGoals).HasMaxLength(4000);
        builder.Property(p => p.RisksLimitations).HasMaxLength(4000);
        builder.Property(p => p.MechanicsPlan).HasMaxLength(4000);
        builder.Property(p => p.AuxiliaryAppliances).HasMaxLength(2000);
        builder.Property(p => p.SpaceManagementPlan).HasMaxLength(2000);
        builder.Property(p => p.InterdisciplinaryPlan).HasMaxLength(2000);
        builder.Property(p => p.PatientDecisionStatus)
            .HasMaxLength(30)
            .HasDefaultValue("NotPresented");
        builder.Property(p => p.PatientDecisionBy).HasMaxLength(200);
        builder.Property(p => p.PatientConsentMethod).HasMaxLength(100);
        builder.Property(p => p.PatientDecisionNotes).HasMaxLength(2000);

        // Relationships
        builder.HasOne(p => p.OrthoCase)
            .WithMany(c => c.TreatmentPlans)
            .HasForeignKey(p => p.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.ApprovedByDoctor)
            .WithMany()
            .HasForeignKey(p => p.ApprovedBy)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
