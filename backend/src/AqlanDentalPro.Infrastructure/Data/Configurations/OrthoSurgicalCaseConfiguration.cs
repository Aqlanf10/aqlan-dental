using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// FluentAPI configuration for the Ortho-Surgical bridge entities. Relationships to
/// existing entities use WithMany() with NO inverse navigation, so Patient/OrthoCase/
/// CephAnalysis/SurgeryCase/Doctor are not modified. Restrict on the mandatory principals
/// (a bridge case must not orphan), SetNull on the optional ones.
/// </summary>
public class OrthoSurgicalCaseConfiguration : IEntityTypeConfiguration<OrthoSurgicalCase>
{
    public void Configure(EntityTypeBuilder<OrthoSurgicalCase> builder)
    {
        builder.Property(c => c.CaseNumber).HasMaxLength(30).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(c => c.DiagnosisSummary).HasMaxLength(4000);

        builder.HasIndex(c => c.CaseNumber).IsUnique();
        builder.HasIndex(c => c.PatientId);
        builder.HasIndex(c => c.OrthoCaseId);
        builder.HasIndex(c => c.SurgeryCaseId);
        builder.HasIndex(c => c.Status);

        builder.HasOne(c => c.Patient)
            .WithMany()
            .HasForeignKey(c => c.PatientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.OrthoCase)
            .WithMany()
            .HasForeignKey(c => c.OrthoCaseId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.CephAnalysis)
            .WithMany()
            .HasForeignKey(c => c.CephAnalysisId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.SurgeryCase)
            .WithMany()
            .HasForeignKey(c => c.SurgeryCaseId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Orthodontist)
            .WithMany()
            .HasForeignKey(c => c.OrthodontistId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.Surgeon)
            .WithMany()
            .HasForeignKey(c => c.SurgeonId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(c => c.SurgeonReview)
            .WithOne(r => r.OrthoSurgicalCase)
            .HasForeignKey<SurgeonReview>(r => r.OrthoSurgicalCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.JointPlan)
            .WithOne(j => j.OrthoSurgicalCase)
            .HasForeignKey<JointPlan>(j => j.OrthoSurgicalCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.Comments)
            .WithOne(m => m.OrthoSurgicalCase)
            .HasForeignKey(m => m.OrthoSurgicalCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrthoSurgicalCommentConfiguration : IEntityTypeConfiguration<OrthoSurgicalComment>
{
    public void Configure(EntityTypeBuilder<OrthoSurgicalComment> builder)
    {
        builder.Property(m => m.AuthorRole).HasMaxLength(40);
        builder.Property(m => m.Body).HasMaxLength(2000).IsRequired();
        builder.HasIndex(m => m.OrthoSurgicalCaseId);
    }
}

public class SurgeonReviewConfiguration : IEntityTypeConfiguration<SurgeonReview>
{
    public void Configure(EntityTypeBuilder<SurgeonReview> builder)
    {
        builder.Property(r => r.Decision).HasMaxLength(40).IsRequired();
        builder.Property(r => r.ProposedProcedure).HasMaxLength(2000);
        builder.Property(r => r.RequiredRecords).HasMaxLength(2000);
        builder.Property(r => r.Risks).HasMaxLength(2000);
        builder.Property(r => r.Notes).HasMaxLength(4000);
        builder.HasIndex(r => r.OrthoSurgicalCaseId).IsUnique();
    }
}

public class JointPlanConfiguration : IEntityTypeConfiguration<JointPlan>
{
    public void Configure(EntityTypeBuilder<JointPlan> builder)
    {
        builder.Property(j => j.OrthodonticObjectives).HasMaxLength(4000);
        builder.Property(j => j.SurgicalObjectives).HasMaxLength(4000);
        builder.Property(j => j.ProcedureType).HasMaxLength(200);
        builder.Property(j => j.Timing).HasMaxLength(500);
        builder.Property(j => j.PreSurgicalRequirements).HasMaxLength(4000);
        builder.Property(j => j.PostSurgicalPlan).HasMaxLength(4000);
        builder.Property(j => j.Risks).HasMaxLength(4000);
        builder.Property(j => j.PatientExplanation).HasMaxLength(4000);
        builder.HasIndex(j => j.OrthoSurgicalCaseId).IsUnique();
    }
}
