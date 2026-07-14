using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephLandmarkConfiguration : IEntityTypeConfiguration<CephLandmark>
{
    public void Configure(EntityTypeBuilder<CephLandmark> builder)
    {
        builder.Property(item => item.PlacementSource)
            .HasMaxLength(30)
            .HasDefaultValue("manual")
            .IsRequired();
        builder.Property(item => item.SourceLandmarkKey).HasMaxLength(100);
        builder.Property(item => item.SourceModelId).HasMaxLength(100);
        builder.Property(item => item.Reasoning).HasMaxLength(200);
        builder.Property(item => item.AiProposalXCoord).HasPrecision(12, 4);
        builder.Property(item => item.AiProposalYCoord).HasPrecision(12, 4);
        builder.Property(item => item.ReviewErrorMm).HasPrecision(10, 4);

        builder.HasIndex(item => new
        {
            item.SourceModelId,
            item.IsReviewed,
            item.IsActive,
        });
    }
}
