using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class PhotoAnalysisConfiguration : IEntityTypeConfiguration<PhotoAnalysis>
{
    public void Configure(EntityTypeBuilder<PhotoAnalysis> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("PhotoAnalyses");

        builder.Property(p => p.ViewType).HasMaxLength(20).IsRequired();
        builder.Property(p => p.ImageFileUrl).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.LandmarksJson).IsRequired(false);
        builder.Property(p => p.MeasurementsJson).IsRequired(false);
        builder.Property(p => p.Notes).HasMaxLength(2000).IsRequired(false);

        builder.HasIndex(p => p.OrthoCaseId);

        // FK to the case (no navigation collection needed on OrthoCase).
        builder.HasOne(p => p.OrthoCase)
            .WithMany()
            .HasForeignKey(p => p.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
