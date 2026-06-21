using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class CephNormConfiguration : IEntityTypeConfiguration<CephNorm>
{
    public void Configure(EntityTypeBuilder<CephNorm> builder)
    {
        builder.ToTable("CephNorms");

        builder.Property(n => n.MeasurementName)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(n => n.NameAr)
            .HasMaxLength(200);

        builder.Property(n => n.AnalysisGroup)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(n => n.Unit)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(n => n.Category)
            .HasMaxLength(30);

        builder.Property(n => n.InterpretationBelow)
            .HasMaxLength(300);

        builder.Property(n => n.InterpretationNormal)
            .HasMaxLength(300);

        builder.Property(n => n.InterpretationAbove)
            .HasMaxLength(300);

        // CLIN-10 — age/gender stratification columns (nullable, backward compatible).
        // A row with all three null is "un-stratified" and matches any patient
        // (the pre-CLIN-10 behavior); the lookup falls back to it when no
        // age/sex-specific row matches.
        builder.Property(n => n.AgeMin)
            .HasColumnType("integer");

        builder.Property(n => n.AgeMax)
            .HasColumnType("integer");

        builder.Property(n => n.Sex)
            .HasMaxLength(1)
            .HasColumnType("character varying(1)");

        // CLIN-10 — replaced the legacy unique index on (MeasurementName,
        // AnalysisGroup) with a non-unique composite index that supports
        // stratified norms (multiple rows per measurement/group, one per
        // age-band × sex) AND the best-match lookup query.
        // The unique-by-strata invariant (at most one row per
        // (MeasurementName, AnalysisGroup, AgeMin, AgeMax, Sex) tuple) is
        // enforced in CephNormSeeder; an admin creating duplicate rows via
        // the API would simply shadow each other at lookup time (the
        // best-match is deterministic by sort order).
        builder.HasIndex(n => new { n.MeasurementName, n.AnalysisGroup, n.AgeMin, n.AgeMax, n.Sex })
            .HasDatabaseName("IX_CephNorms_MeasurementName_AnalysisGroup_AgeMin_AgeMax_Sex");
    }
}
