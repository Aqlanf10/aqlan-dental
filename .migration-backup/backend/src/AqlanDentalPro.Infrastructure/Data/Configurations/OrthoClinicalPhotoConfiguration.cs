using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public class OrthoClinicalPhotoConfiguration : IEntityTypeConfiguration<OrthoClinicalPhoto>
{
    public void Configure(EntityTypeBuilder<OrthoClinicalPhoto> builder)
    {
        builder.HasKey(p => p.Id);

        builder.ToTable("OrthoClinicalPhotos");

        builder.Property(p => p.PhotoUrl).HasMaxLength(1000).IsRequired();
        builder.Property(p => p.PhotoType).HasMaxLength(50).IsRequired();
        builder.Property(p => p.Caption).HasMaxLength(500).IsRequired(false);
        builder.Property(p => p.Category).HasMaxLength(50).IsRequired(false);
        builder.Property(p => p.Subtype).HasMaxLength(100).IsRequired(false);
        builder.Property(p => p.TreatmentPhase).HasMaxLength(20).IsRequired(false);
        builder.Property(p => p.IsSelectedForReport).HasDefaultValue(false);

        builder.HasIndex(p => p.OrthoCaseId);
        builder.HasIndex(p => new { p.OrthoCaseId, p.Category });

        builder.HasOne(p => p.OrthoCase)
            .WithMany(c => c.OrthoClinicalPhotos)
            .HasForeignKey(p => p.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
