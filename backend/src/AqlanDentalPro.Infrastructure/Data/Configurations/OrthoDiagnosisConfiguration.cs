using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class OrthoDiagnosisConfiguration : IEntityTypeConfiguration<OrthoDiagnosis>
{
    public void Configure(EntityTypeBuilder<OrthoDiagnosis> builder)
    {
        builder.HasKey(d => d.Id);

        builder.ToTable("OrthoDiagnoses");

        builder.Property(d => d.SkeletalClassification).HasMaxLength(50).IsRequired(false);
        builder.Property(d => d.DentalClassification).HasMaxLength(50).IsRequired(false);
        builder.Property(d => d.FacialPattern).HasMaxLength(50).IsRequired(false);
        builder.Property(d => d.ANB).HasPrecision(6, 2).IsRequired(false);
        builder.Property(d => d.Wits).HasPrecision(6, 2).IsRequired(false);
        builder.Property(d => d.FMA).HasPrecision(6, 2).IsRequired(false);
        builder.Property(d => d.SNA).HasPrecision(6, 2).IsRequired(false);
        builder.Property(d => d.SNB).HasPrecision(6, 2).IsRequired(false);
        builder.Property(d => d.IMPA).HasPrecision(6, 2).IsRequired(false);
        builder.Property(d => d.Summary).IsRequired(false);

        builder.HasIndex(d => d.OrthoCaseId).IsUnique();

        builder.HasOne(d => d.OrthoCase)
            .WithOne(c => c.Diagnosis)
            .HasForeignKey<OrthoDiagnosis>(d => d.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
