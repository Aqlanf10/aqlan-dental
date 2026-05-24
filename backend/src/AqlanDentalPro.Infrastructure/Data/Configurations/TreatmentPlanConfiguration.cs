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
        // Performance indexes
        builder.HasIndex(p => p.OrthoCaseId);

        // PlanLabel for Plan A/B/C
        builder.Property(p => p.PlanLabel).HasMaxLength(5).HasDefaultValue("A");

        // Relationships
        builder.HasOne(p => p.OrthoCase)
            .WithMany(c => c.TreatmentPlans)
            .HasForeignKey(p => p.OrthoCaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
