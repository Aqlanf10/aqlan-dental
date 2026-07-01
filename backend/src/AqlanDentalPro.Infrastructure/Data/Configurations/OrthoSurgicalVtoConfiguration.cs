using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// FluentAPI configuration for the Surgical VTO (Visual Treatment Objective) scenarios on
/// an Ortho-Surgical case (Sprint A9). Relationships to existing entities use WithMany()
/// with NO inverse navigation, so <see cref="OrthoSurgicalCase"/> and <see cref="CephAnalysis"/>
/// are not modified. FK to OrthoSurgicalCases is ON DELETE CASCADE (a deleted case takes its
/// VTO scenarios with it); FK to CephAnalyses is ON DELETE SET NULL (the baseline analysis may
/// be archived without losing the stored scenario — predicted values are already snapshotted).
/// </summary>
public class OrthoSurgicalVtoConfiguration : IEntityTypeConfiguration<OrthoSurgicalVto>
{
    public void Configure(EntityTypeBuilder<OrthoSurgicalVto> builder)
    {
        builder.ToTable("OrthoSurgicalVtos");

        builder.Property(v => v.Notes).HasMaxLength(4000);

        // decimal precision mirrors the rest of the ceph module (numeric(6,2) — see OrthoDiagnosis).
        builder.Property(v => v.MaxillaMoveMm).HasPrecision(6, 2);
        builder.Property(v => v.MandibleMoveMm).HasPrecision(6, 2);
        builder.Property(v => v.ChinMoveMm).HasPrecision(6, 2);
        builder.Property(v => v.RotationDegree).HasPrecision(6, 2);
        builder.Property(v => v.PredictedSNA).HasPrecision(6, 2);
        builder.Property(v => v.PredictedSNB).HasPrecision(6, 2);
        builder.Property(v => v.PredictedANB).HasPrecision(6, 2);
        builder.Property(v => v.PredictedWits).HasPrecision(6, 2);
        builder.Property(v => v.PredictedOverjet).HasPrecision(6, 2);

        builder.HasIndex(v => v.OrthoSurgicalCaseId);

        builder.HasOne(v => v.OrthoSurgicalCase)
            .WithMany()
            .HasForeignKey(v => v.OrthoSurgicalCaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.CephAnalysis)
            .WithMany()
            .HasForeignKey(v => v.CephAnalysisId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
