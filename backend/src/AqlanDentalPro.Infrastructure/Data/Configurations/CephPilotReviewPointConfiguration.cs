using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotReviewPointConfiguration : IEntityTypeConfiguration<CephPilotReviewPoint>
{
    public void Configure(EntityTypeBuilder<CephPilotReviewPoint> builder)
    {
        builder.ToTable("CephPilotReviewPoints");
        builder.Property(item => item.LandmarkKey).HasMaxLength(12).IsRequired();
        builder.Property(item => item.Visibility).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(item => item.XCoordPx).HasPrecision(12, 4);
        builder.Property(item => item.YCoordPx).HasPrecision(12, 4);
        builder.Property(item => item.ContourDecision).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.HasIndex(item => new { item.ReviewSessionId, item.LandmarkKey }).IsUnique();
        builder.HasOne(item => item.ReviewSession)
            .WithMany(item => item.Points)
            .HasForeignKey(item => item.ReviewSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
