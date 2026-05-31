using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M5 FIX: FluentAPI configuration for TreatmentStage entity.
/// Index on OrthoCaseId, string constraints.
/// </summary>
public class TreatmentStageConfiguration : IEntityTypeConfiguration<TreatmentStage>
{
    public void Configure(EntityTypeBuilder<TreatmentStage> builder)
    {
        builder.Property(s => s.StageName).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Status).HasMaxLength(20).IsRequired();
        builder.Property(s => s.Notes).HasMaxLength(500);

        // Performance indexes
        builder.HasIndex(s => s.OrthoCaseId);

        // Relationships
        builder.HasOne(s => s.OrthoCase)
            .WithMany(c => c.Stages)
            .HasForeignKey(s => s.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
