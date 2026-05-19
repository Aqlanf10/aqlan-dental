using AqlanDentalPro.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AqlanDentalPro.Infrastructure.Data.Configurations;

/// <summary>
/// M5 FIX: FluentAPI configuration for TreatmentPlan entity.
/// Index on OrthoCaseId, string constraints, FK.
/// </summary>
public class TreatmentPlanConfiguration : IEntityTypeConfiguration<TreatmentPlan>
{
    public void Configure(EntityTypeBuilder<TreatmentPlan> builder)
    {
        builder.Property(p => p.PlanName).HasMaxLength(200);
        builder.Property(p => p.Status).HasMaxLength(20).IsRequired();
        builder.Property(p => p.Notes).HasMaxLength(1000);

        // Performance indexes
        builder.HasIndex(p => p.OrthoCaseId);

        // Relationships
        builder.HasOne(p => p.OrthoCase)
            .WithMany(c => c.TreatmentPlans)
            .HasForeignKey(p => p.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
