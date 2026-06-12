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

        // One norm row per measurement per analysis group.
        builder.HasIndex(n => new { n.MeasurementName, n.AnalysisGroup })
            .IsUnique();
    }
}
