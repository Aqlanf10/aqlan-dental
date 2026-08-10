using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

public sealed class CephPilotReviewPointConfiguration : IEntityTypeConfiguration<CephPilotReviewPoint>
{
    public void Configure(EntityTypeBuilder<CephPilotReviewPoint> builder)
    {
        // Mirrors migration 20260719000000_AddCephPilotBlindedReviews.
        //
        // This one keeps the reviewer's answers internally consistent: a landmark recorded as
        // Visible must carry coordinates, and one recorded as NotVisible must carry none.
        // Without it a fresh database would accept "visible, but nowhere" — a row that reads as
        // a real observation and silently poisons any measurement derived from it.
        builder.ToTable("CephPilotReviewPoints", table =>
            table.HasCheckConstraint(
                "CK_CephPilotReviewPoints_CoordinateContract",
                "(\"Visibility\" = 'Visible' AND \"XCoordPx\" IS NOT NULL AND \"YCoordPx\" IS NOT NULL) OR (\"Visibility\" = 'NotVisible' AND \"XCoordPx\" IS NULL AND \"YCoordPx\" IS NULL)"));
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
