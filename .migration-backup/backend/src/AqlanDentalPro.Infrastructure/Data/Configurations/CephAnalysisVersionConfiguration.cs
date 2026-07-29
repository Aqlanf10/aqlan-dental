using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// CEPH-EPIC batch C-B — schema for ceph analysis version snapshots.
/// The FK to CephAnalyses is cascade-delete: deleting an analysis removes its
/// snapshots. SnapshotDate is indexed so the versions panel can order by date
/// cheaply. CephAnalysisId is also indexed for the list-by-analysis query.
/// </summary>
public class CephAnalysisVersionConfiguration : IEntityTypeConfiguration<CephAnalysisVersion>
{
    public void Configure(EntityTypeBuilder<CephAnalysisVersion> builder)
    {
        builder.ToTable("CephAnalysisVersions");

        builder.Property(v => v.Label)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(v => v.LandmarksJson)
            .IsRequired();

        builder.Property(v => v.MeasurementsJson)
            .IsRequired();

        builder.Property(v => v.DiagnosisJson);

        builder.Property(v => v.SnapshotDate)
            .IsRequired();

        builder.HasIndex(v => v.CephAnalysisId);
        builder.HasIndex(v => new { v.CephAnalysisId, v.SnapshotDate });

        builder.HasOne(v => v.Analysis)
            .WithMany()
            .HasForeignKey(v => v.CephAnalysisId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
