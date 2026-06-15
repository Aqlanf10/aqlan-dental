using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class OrthoImagePreparationConfiguration : IEntityTypeConfiguration<OrthoImagePreparation>
{
    public void Configure(EntityTypeBuilder<OrthoImagePreparation> builder)
    {
        builder.HasKey(p => p.Id);
        builder.ToTable("OrthoImagePreparations");

        builder.Property(p => p.CropX).HasPrecision(6, 5);
        builder.Property(p => p.CropY).HasPrecision(6, 5);
        builder.Property(p => p.CropWidth).HasPrecision(6, 5).HasDefaultValue(1m);
        builder.Property(p => p.CropHeight).HasPrecision(6, 5).HasDefaultValue(1m);
        builder.Property(p => p.Zoom).HasPrecision(5, 2).HasDefaultValue(1m);
        builder.Property(p => p.AspectRatio).HasMaxLength(20).HasDefaultValue("Original");
        builder.Property(p => p.Preset).HasMaxLength(50);
        builder.Property(p => p.Status).HasMaxLength(40).HasDefaultValue("PreparedForReport");
        builder.Property(p => p.PreparedImageUrl).HasMaxLength(1000);

        builder.HasIndex(p => p.OrthoClinicalPhotoId).IsUnique();
        builder.HasIndex(p => p.Status);

        builder.HasOne(p => p.OrthoClinicalPhoto)
            .WithOne(p => p.ImagePreparation)
            .HasForeignKey<OrthoImagePreparation>(p => p.OrthoClinicalPhotoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
